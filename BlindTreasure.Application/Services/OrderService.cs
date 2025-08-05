using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BlindTreasure.Application.Services;

public class OrderService : IOrderService
{
    private readonly ICacheService _cacheService;
    private readonly ICartItemService _cartItemService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IProductService _productService;
    private readonly IPromotionService _promotionService;
    private readonly IStripeService _stripeService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGhnShippingService _ghnShippingService;

    public OrderService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IProductService productService,
        IUnitOfWork unitOfWork,
        ICartItemService cartItemService,
        IStripeService stripeService,
        IPromotionService promotionService,
        IGhnShippingService ghnShippingService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _cartItemService = cartItemService;
        _stripeService = stripeService;
        _promotionService = promotionService;
        _ghnShippingService = ghnShippingService;
    }

    public async Task<string> CheckoutAsync(CreateCheckoutRequestDto dto)
    {
        _loggerService.Info("Start checkout from system cart.");
        var cart = await _cartItemService.GetCurrentUserCartAsync();
        if (cart.SellerItems == null || !cart.SellerItems.Any())
        {
            _loggerService.Warn("Cart is empty.");
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmpty);
        }

        // Lọc product vật lý
        var hasProduct = cart.SellerItems.Any(s => s.Items.Any(i => i.ProductId.HasValue));
        var hasBlindBox = cart.SellerItems.Any(s => s.Items.Any(i => i.BlindBoxId.HasValue));

        Guid? shippingAddressId = null;
        if (dto.IsShip == true)
        {
            if (!hasProduct)
            {
                _loggerService.Warn("Cart only contains BlindBox, cannot ship.");
                throw ErrorHelper.BadRequest("Không thể giao hàng: Giỏ hàng không có sản phẩm vật lý nào để ship.");
            }

            var userId = _claimsService.CurrentUserId;
            var address = await _unitOfWork.Addresses.GetQueryable()
                .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
                .FirstOrDefaultAsync();
            if (address == null)
            {
                _loggerService.Warn("Default shipping address not found.");
                throw ErrorHelper.BadRequest("Không tìm thấy địa chỉ mặc định của khách hàng.");
            }

            shippingAddressId = address.Id;
        }

        // Gộp lại thành list item
        var groups = cart.SellerItems
            .Select(s => new SellerCheckoutGroup
            {
                SellerId = s.SellerId,
                PromotionId = s.PromotionId,
                Items = s.Items.Select(i => new CheckoutItem
                {
                    SellerId = s.SellerId,
                    ProductId = i.ProductId,
                    BlindBoxId = i.BlindBoxId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            })
            .ToList();

        _loggerService.Success("Checkout from system cart completed.");

        return await CheckoutCore(groups, shippingAddressId);
    }

    public async Task<string> CheckoutFromClientCartAsync(DirectCartCheckoutDto cartDto)
    {
        _loggerService.Info("Start checkout from client cart (new format).");

        if (cartDto == null || cartDto.SellerItems == null || !cartDto.SellerItems.Any())
            throw ErrorHelper.BadRequest(ErrorMessages.OrderClientCartInvalid);

        Guid? shippingAddressId = null;
        if (cartDto.IsShip == true)
        {
            var hasProduct = cartDto.SellerItems.Any(s => s.Items.Any(i => i.ProductId.HasValue));
            if (!hasProduct)
                throw ErrorHelper.BadRequest("Không thể giao hàng: Không có sản phẩm vật lý nào.");

            var userId = _claimsService.CurrentUserId;
            var address = await _unitOfWork.Addresses.GetQueryable()
                .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
                .FirstOrDefaultAsync();
            if (address == null)
                throw ErrorHelper.BadRequest("Không tìm thấy địa chỉ mặc định của khách hàng.");
            shippingAddressId = address.Id;
        }

        // Gộp lại thành list item
        // Build groups
        var groups = cartDto.SellerItems
            .Select(s => new SellerCheckoutGroup
            {
                SellerId = s.SellerId,
                PromotionId = s.PromotionId,
                Items = s.Items.Select(i => new CheckoutItem
                {
                    SellerId = s.SellerId,
                    ProductId = i.ProductId,
                    BlindBoxId = i.BlindBoxId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            })
            .ToList();

        var result = await CheckoutCore(groups, shippingAddressId);
        return result;
    }

    public async Task<Pagination<OrderDetailDto>> GetMyOrderDetailsAsync(OrderDetailQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;
        var query = _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Include(od => od.Product)
            .Include(od => od.BlindBox)
            .Include(od => od.Shipments)
            .Include(od => od.InventoryItems)
            .Where(od => od.Order.UserId == userId && !od.Order.IsDeleted);

        if (param.Status.HasValue)
            query = query.Where(od => od.Status == param.Status.Value);
        if (param.OrderId.HasValue)
            query = query.Where(od => od.OrderId == param.OrderId.Value);
        if (param.MinPrice.HasValue)
            query = query.Where(od => od.UnitPrice >= param.MinPrice.Value);
        if (param.MaxPrice.HasValue)
            query = query.Where(od => od.UnitPrice <= param.MaxPrice.Value);
        if (param.IsBlindBox == true)
            query = query.Where(od => od.BlindBoxId != null);
        if (param.IsProduct == true)
            query = query.Where(od => od.ProductId != null);

        query = param.Desc
            ? query.OrderByDescending(od => od.Order.UpdatedAt ?? od.Order.CreatedAt)
            : query.OrderBy(od => od.Order.UpdatedAt ?? od.Order.CreatedAt);

        var totalCount = await query.CountAsync();
        var orderDetails = param.PageIndex == 0
            ? await query.ToListAsync()
            : await query.Skip((param.PageIndex - 1) * param.PageSize).Take(param.PageSize).ToListAsync();

        var dtos = orderDetails.Select(OrderDtoMapper.ToOrderDetailDtoFullIncluded).ToList();
        return new Pagination<OrderDetailDto>(dtos, totalCount, param.PageIndex, param.PageSize);
    }

    public async Task<OrderDto> GetOrderByIdAsync(Guid orderId)
    {
        var userId = _claimsService.CurrentUserId;
        var cacheKey = $"order:user:{userId}:order:{orderId}";
        var cached = await _cacheService.GetAsync<OrderDto>(cacheKey);
        if (cached != null)
        {
            _loggerService.Info($"Order {orderId} loaded from cache.");
            return cached;
        }

        var order = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.BlindBox)
            .Include(o => o.ShippingAddress)
            .Include(o => o.Payment).ThenInclude(p => p.Transactions)
            .OrderByDescending(o => o.PlacedAt)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            _loggerService.Warn($"Order {orderId} not found.");
            throw ErrorHelper.NotFound(ErrorMessages.OrderNotFound);
        }

        var dto = OrderDtoMapper.ToOrderDto(order);
        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        _loggerService.Info($"Order {orderId} loaded and cached.");
        return dto;
    }

    public async Task<Pagination<OrderDto>> GetMyOrdersAsync(OrderQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;
        var query = _unitOfWork.Orders.GetQueryable()
            .Where(o => o.UserId == userId && !o.IsDeleted)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Shipments)
            .Include(o => o.OrderDetails).ThenInclude(od => od.BlindBox)
            .Include(o => o.ShippingAddress)
            .Include(o => o.Payment).ThenInclude(p => p.Transactions)
            .AsNoTracking();

        if (param.Status.HasValue)
            query = query.Where(o => o.Status == param.Status.Value.ToString());
        if (param.PlacedFrom.HasValue)
            query = query.Where(o => o.PlacedAt >= param.PlacedFrom.Value);
        if (param.PlacedTo.HasValue)
            query = query.Where(o => o.PlacedAt <= param.PlacedTo.Value);

        query = param.Desc
            ? query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
            : query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);

        var totalCount = await query.CountAsync();
        var orders = param.PageIndex == 0
            ? await query.ToListAsync()
            : await query.Skip((param.PageIndex - 1) * param.PageSize).Take(param.PageSize).ToListAsync();

        var dtos = orders.Select(OrderDtoMapper.ToOrderDto).ToList();
        _loggerService.Info("Loaded order list for current user.");
        return new Pagination<OrderDto>(dtos, totalCount, param.PageIndex, param.PageSize);
    }

    public async Task CancelOrderAsync(Guid orderId)
    {
        var userId = _claimsService.CurrentUserId;
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, o => o.OrderDetails);
        if (order == null || order.IsDeleted || order.UserId != userId)
        {
            _loggerService.Warn($"Order {orderId} not found or not belong to user.");
            throw ErrorHelper.NotFound(ErrorMessages.OrderNotFound);
        }

        if (order.Status != OrderStatus.PENDING.ToString())
        {
            _loggerService.Warn($"Order {orderId} is not pending, cannot cancel.");
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCancelOnlyPending);
        }

        order.Status = OrderStatus.CANCELLED.ToString();
        order.UpdatedAt = DateTime.UtcNow;

        foreach (var od in order.OrderDetails)
        {
            if (od.ProductId.HasValue)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(od.ProductId.Value);
                product.Stock += od.Quantity;
                await _unitOfWork.Products.Update(product);
            }
            else if (od.BlindBoxId.HasValue)
            {
                var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(od.BlindBoxId.Value);
                blindBox.TotalQuantity += od.Quantity;
                await _unitOfWork.BlindBoxes.Update(blindBox);
            }

            od.Status = OrderDetailItemStatus.CANCELLED;
        }

        await _unitOfWork.Orders.Update(order);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Success($"Order {orderId} cancelled successfully.");
    }

    public async Task DeleteOrderAsync(Guid orderId)
    {
        var userId = _claimsService.CurrentUserId;
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null || order.IsDeleted || order.UserId != userId)
        {
            _loggerService.Warn($"Order {orderId} not found or not belong to user.");
            throw ErrorHelper.NotFound(ErrorMessages.OrderNotFound);
        }

        order.IsDeleted = true;
        order.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.Orders.Update(order);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Success($"Order {orderId} deleted (soft) successfully.");
    }


    private async Task<string> CheckoutCore(
        List<SellerCheckoutGroup> groups,
        Guid? shippingAddressId)
    {
        _loggerService.Info("Start core checkout logic.");

        // 1. Validate user
        var userId = _claimsService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.Forbidden(ErrorMessages.AccountNotFound);

        // 2. Validate cart
        if (groups == null || !groups.Any(g => g.Items?.Any() == true))
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmptyOrInvalid);

        // 3. Validate shipping requirement
        var hasPhysical = groups.SelectMany(g => g.Items).Any(i => i.ProductId.HasValue);
        if (shippingAddressId.HasValue && !hasPhysical)
            throw ErrorHelper.BadRequest("Cannot ship: no physical products in cart.");

        Address? shippingAddress = null;
        if (shippingAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses.GetByIdAsync(shippingAddressId.Value);
            if (shippingAddress == null || shippingAddress.IsDeleted || shippingAddress.UserId != userId)
                throw ErrorHelper.BadRequest(ErrorMessages.OrderShippingAddressInvalid);
        }

        // 4. Preload products and blindboxes
        var productIds = groups.SelectMany(g => g.Items)
            .Where(i => i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .Distinct();
        var blindBoxIds = groups.SelectMany(g => g.Items)
            .Where(i => i.BlindBoxId.HasValue)
            .Select(i => i.BlindBoxId!.Value)
            .Distinct();

        var products = await _unitOfWork.Products.GetQueryable()
            .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
            .Include(p => p.Seller)
            .ToListAsync();
        var blindBoxes = await _unitOfWork.BlindBoxes.GetQueryable()
            .Where(b => blindBoxIds.Contains(b.Id) && !b.IsDeleted)
            .Include(b => b.Seller)
            .ToListAsync();

        var prodById = products.ToDictionary(p => p.Id);
        var boxById = blindBoxes.ToDictionary(b => b.Id);

        // 5. Create order
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.PENDING.ToString(),
            PlacedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ShippingAddressId = shippingAddressId,
            OrderDetails = new List<OrderDetail>(),
            OrderSellerPromotions = new List<OrderSellerPromotion>()
        };

        // 6. Build details and reserve stock (WITHOUT promotion calculation first)
        foreach (var group in groups)
        foreach (var item in group.Items)
        {
            decimal unitPrice;
            string itemName;
            if (item.ProductId.HasValue)
            {
                var p = prodById[item.ProductId.Value];
                if (p.Status != ProductStatus.Active || p.Stock < item.Quantity)
                    throw ErrorHelper.BadRequest($"Product {p.Name} invalid or out of stock.");
                unitPrice = p.Price;
                itemName = p.Name;
                p.Stock -= item.Quantity;
                await _unitOfWork.Products.Update(p);
            }
            else
            {
                var b = boxById[item.BlindBoxId!.Value];
                if (b.Status != BlindBoxStatus.Approved || b.TotalQuantity < item.Quantity)
                    throw ErrorHelper.BadRequest($"BlindBox {b.Name} invalid or out of stock.");
                unitPrice = b.Price;
                itemName = b.Name;
                b.TotalQuantity -= item.Quantity;
                await _unitOfWork.BlindBoxes.Update(b);
            }

            var detail = new OrderDetail
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = item.ProductId,
                BlindBoxId = item.BlindBoxId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = unitPrice * item.Quantity,
                SellerId = group.SellerId,
                Status = OrderDetailItemStatus.PENDING,
                CreatedAt = DateTime.UtcNow,
                Logs = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Created {itemName}, Qty={item.Quantity}",
                // Initialize new fields
                DetailDiscountPromotion = null,
                FinalDetailPrice = unitPrice * item.Quantity // Initially same as TotalPrice
            };
            order.OrderDetails.Add(detail);
        }

        // 7. Apply promotions AFTER all OrderDetails are created
        foreach (var group in groups)
            if (group.PromotionId.HasValue)
            {
                var promo = await _unitOfWork.Promotions.GetByIdAsync(group.PromotionId.Value);
                if (promo == null || promo.Status != PromotionStatus.Approved)
                    throw ErrorHelper.BadRequest("Invalid promotion");

                // Check if seller participates in promotion
                var participant = await _unitOfWork.PromotionParticipants.GetQueryable()
                    .Where(p => p.PromotionId == promo.Id && p.SellerId == group.SellerId)
                    .FirstOrDefaultAsync();
                if (participant == null)
                {
                    _loggerService.Warn($"Seller {group.SellerId} not participate in promotion {promo.Code}");
                    throw ErrorHelper.BadRequest(
                        $"Promotion not applicable for this seller. ( Seller did not participate to this promotion :{promo.Id}");
                }

                // Get order details for this seller
                var sellerOrderDetails = order.OrderDetails
                    .Where(d => d.SellerId == group.SellerId)
                    .ToList();

                // Apply promotion to individual OrderDetails
                ApplyPromotionToOrderDetails(sellerOrderDetails, promo);

                // Calculate total discount for OrderSellerPromotion record
                var totalDiscount = sellerOrderDetails.Sum(d => d.DetailDiscountPromotion ?? 0);

                order.OrderSellerPromotions.Add(new OrderSellerPromotion
                {
                    Order = order,
                    OrderId = order.Id,
                    SellerId = group.SellerId,
                    Promotion = promo,
                    PromotionId = promo.Id,
                    DiscountAmount = totalDiscount,
                    Note = $"Applied {promo.Code}"
                });

                // Update promotion usage
                promo.UsageLimit = (promo.UsageLimit ?? 0) - 1;
                await _unitOfWork.Promotions.Update(promo);
            }
            else
            {
                // No promotion - ensure FinalDetailPrice equals TotalPrice for this seller's items
                var sellerOrderDetails = order.OrderDetails
                    .Where(d => d.SellerId == group.SellerId)
                    .ToList();

                foreach (var detail in sellerOrderDetails) detail.FinalDetailPrice = detail.TotalPrice;
            }

        // 8. Calculate order totals
        order.TotalAmount = order.OrderDetails.Sum(d => d.TotalPrice);
        var totalPromotionDiscount = order.OrderDetails.Sum(d => d.DetailDiscountPromotion ?? 0);
        order.FinalAmount = order.TotalAmount - totalPromotionDiscount;

        if (order.FinalAmount < 0)
            throw ErrorHelper.BadRequest("Final amount cannot be negative.");

        // 9. Save order and details
        order = await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // 10. Shipments grouped by seller
        if (shippingAddress != null)
        {
            order.TotalShippingFee = 0m;
            var grouped = order.OrderDetails.Where(d => d.ProductId.HasValue).GroupBy(d => d.SellerId);
            foreach (var grp in grouped)
            {
                var seller = grp.First().Product!.Seller;
                var ghnResp = await _ghnShippingService.PreviewOrderAsync(
                    _ghnShippingService.BuildGhnOrderRequest(grp, seller, shippingAddress, od => od.Product!,
                        od => od.Quantity));
                var fee = ghnResp?.TotalFee ?? 0m;
                order.TotalShippingFee += fee;
                order.FinalAmount += fee;
                _loggerService.Info($"GHN shipment for seller {seller.Id}, fee={fee}");

                // Create shipment for the group
                var shipment = new Shipment
                {
                    Provider = "GHN",
                    OrderCode = ghnResp?.OrderCode ?? string.Empty,
                    TotalFee = (int?)ghnResp?.TotalFee ?? 0,
                    MainServiceFee = (int?)ghnResp?.Fee?.MainService ?? 0,
                    TrackingNumber = ghnResp?.OrderCode ?? string.Empty,
                    ShippedAt = DateTime.UtcNow,
                    EstimatedDelivery = ghnResp?.ExpectedDeliveryTime ?? DateTime.UtcNow.AddDays(3),
                    Status = ShipmentStatus.WAITING_PAYMENT
                };
                await _unitOfWork.Shipments.AddAsync(shipment);

                // Link shipment to physical order details of this seller
                foreach (var od in grp)
                {
                    od.Status = OrderDetailItemStatus.PENDING;
                    od.Shipments.Add(shipment); // Many-to-many
                    await _unitOfWork.OrderDetails.Update(od);
                }
            }

            await _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();
        }

        // 11. Cleanup and return
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Success($"Checkout success for user {userId}.");
        var sessionUrl = await _stripeService.CreateCheckoutSession(order.Id);
        await _cartItemService.UpdateCartAfterCheckoutAsync(userId, groups.SelectMany(g => g.Items).ToList());
        return sessionUrl;
    }

    /// <summary>
    /// Apply promotion discount to individual OrderDetails
    /// </summary>
    private void ApplyPromotionToOrderDetails(List<OrderDetail> orderDetails, Promotion promotion)
    {
        if (!orderDetails.Any()) return;

        var subTotal = orderDetails.Sum(d => d.TotalPrice);

        if (promotion.DiscountType == DiscountType.Percentage)
        {
            // Percentage: Apply directly to each item
            foreach (var detail in orderDetails)
            {
                var itemDiscount = Math.Round(detail.TotalPrice * promotion.DiscountValue / 100m, 2);
                detail.DetailDiscountPromotion = itemDiscount;
                detail.FinalDetailPrice = detail.TotalPrice - itemDiscount;
            }
        }
        else // Fixed Amount
        {
            // Fixed: Distribute proportionally
            var totalDiscount = Math.Min(promotion.DiscountValue, subTotal);
            var remainingDiscount = totalDiscount;

            for (var i = 0; i < orderDetails.Count; i++)
            {
                var detail = orderDetails[i];

                if (i == orderDetails.Count - 1)
                {
                    // Last item gets remaining discount to avoid rounding errors
                    detail.DetailDiscountPromotion = remainingDiscount;
                    detail.FinalDetailPrice = detail.TotalPrice - remainingDiscount;
                }
                else
                {
                    var proportion = detail.TotalPrice / subTotal;
                    var itemDiscount = Math.Round(totalDiscount * proportion, 2);
                    detail.DetailDiscountPromotion = itemDiscount;
                    detail.FinalDetailPrice = detail.TotalPrice - itemDiscount;
                    remainingDiscount -= itemDiscount;
                }
            }
        }

        // Log promotion application
        foreach (var detail in orderDetails)
            detail.Logs +=
                $"\n [{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Applied promotion {promotion.Id}: - Discount amount: {detail.DetailDiscountPromotion:C}";
    }

    public async Task<List<ShipmentCheckoutResponseDTO>> PreviewShippingCheckoutAsync(
        List<CartSellerItemDto> sellerItems, bool? isPreview = false)
    {
        _loggerService.Info("Preview shipping checkout (by seller items) started.");
        var userId = _claimsService.CurrentUserId;
        if (sellerItems == null || !sellerItems.Any())
            throw ErrorHelper.BadRequest("Cart trống.");

        var address = await _unitOfWork.Addresses.GetQueryable()
            .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
            .FirstOrDefaultAsync();
        if (address == null)
            throw ErrorHelper.BadRequest("Không tìm thấy địa chỉ mặc định của khách hàng.");

        var result = new List<ShipmentCheckoutResponseDTO>();

        foreach (var sellerGroup in sellerItems)
        {
            var productItems = sellerGroup.Items.Where(i => i.ProductId.HasValue).ToList();
            if (!productItems.Any())
                continue;

            var productIds = productItems.Select(i => i.ProductId.Value).ToList();
            var products = await _unitOfWork.Products.GetQueryable()
                .Where(p => productIds.Contains(p.Id))
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .ToListAsync();

            var seller = products.FirstOrDefault()?.Seller;
            if (seller == null)
                continue;

            var itemsWithProduct = productItems.Select(item => new
            {
                Product = products.First(p => p.Id == item.ProductId),
                Quantity = item.Quantity
            });

            var ghnOrderRequest = _ghnShippingService.BuildGhnOrderRequest(
                itemsWithProduct,
                seller,
                address,
                x => x.Product,
                x => x.Quantity
            );

            var ghnPreviewResponse = await _ghnShippingService.PreviewOrderAsync(ghnOrderRequest);

            result.Add(new ShipmentCheckoutResponseDTO
            {
                SellerId = seller.Id,
                SellerCompanyName = seller.CompanyName,
                Shipment = null,
                GhnPreviewResponse = ghnPreviewResponse
            });
        }

        _loggerService.Info("Preview shipping checkout (by seller items) completed.");
        return result;
    }


    //============================================//

    private async Task<string> NewCheckoutCore(
        List<SellerCheckoutGroup> groups,
        Guid? shippingAddressId)
    {
        _loggerService.Info("Start core checkout logic.");

        // 1. Validate user
        var userId = _claimsService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.Forbidden(ErrorMessages.AccountNotFound);

        // 2. Validate cart
        if (groups == null || !groups.Any(g => g.Items?.Any() == true))
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmptyOrInvalid);

        // 3. Validate shipping
        var hasPhysical = groups.SelectMany(g => g.Items).Any(i => i.ProductId.HasValue);
        if (shippingAddressId.HasValue && !hasPhysical)
            throw ErrorHelper.BadRequest("Cannot ship: no physical products in cart.");

        Address shippingAddress = null;
        if (shippingAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses.GetByIdAsync(shippingAddressId.Value);
            if (shippingAddress == null || shippingAddress.IsDeleted || shippingAddress.UserId != userId)
                throw ErrorHelper.BadRequest(ErrorMessages.OrderShippingAddressInvalid);
        }

        // 4. Preload data
        var productIds = groups.SelectMany(g => g.Items)
            .Where(i => i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .Distinct().ToList();

        var blindBoxIds = groups.SelectMany(g => g.Items)
            .Where(i => i.BlindBoxId.HasValue)
            .Select(i => i.BlindBoxId!.Value)
            .Distinct().ToList();

        // Load products and blindboxes in parallel
        var productsTask = _unitOfWork.Products.GetQueryable()
            .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
            .Include(p => p.Seller)
            .ToListAsync();

        var blindBoxesTask = _unitOfWork.BlindBoxes.GetQueryable()
            .Where(b => blindBoxIds.Contains(b.Id) && !b.IsDeleted)
            .Include(b => b.Seller)
            .ToListAsync();

        await Task.WhenAll(productsTask, blindBoxesTask);
        var products = await productsTask;
        var blindBoxes = await blindBoxesTask;

        var prodById = products.ToDictionary(p => p.Id);
        var boxById = blindBoxes.ToDictionary(b => b.Id);

        // Preload sellers
        var sellerIds = products.Select(p => p.SellerId)
            .Concat(blindBoxes.Select(b => b.SellerId))
            .Distinct().ToList();
        var sellerCache = await _unitOfWork.Sellers.GetQueryable()
            .Where(s => sellerIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        // 5. Create order
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.PENDING.ToString(),
            PlacedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ShippingAddressId = shippingAddressId,
            OrderDetails = new List<OrderDetail>(),
            OrderSellerPromotions = new List<OrderSellerPromotion>()
        };

        // 6. Build order details
        foreach (var group in groups)
        foreach (var item in group.Items)
        {
            decimal unitPrice;
            string itemName;
            var sellerId = group.SellerId;
            var isProduct = item.ProductId.HasValue;

            if (isProduct)
            {
                var product = prodById[item.ProductId!.Value];
                if (product.Status != ProductStatus.Active || product.Stock < item.Quantity)
                    throw ErrorHelper.BadRequest($"Product {product.Name} invalid or out of stock.");

                unitPrice = product.Price;
                itemName = product.Name;
                sellerId = product.SellerId;
            }
            else
            {
                var blindBox = boxById[item.BlindBoxId!.Value];
                if (blindBox.Status != BlindBoxStatus.Approved || blindBox.TotalQuantity < item.Quantity)
                    throw ErrorHelper.BadRequest($"BlindBox {blindBox.Name} invalid or out of stock.");

                unitPrice = blindBox.Price;
                itemName = blindBox.Name;
                sellerId = blindBox.SellerId;
            }

            order.OrderDetails.Add(new OrderDetail
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                BlindBoxId = item.BlindBoxId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = unitPrice * item.Quantity,
                SellerId = sellerId,
                Status = OrderDetailItemStatus.PENDING,
                CreatedAt = DateTime.UtcNow,
                Logs = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Created {itemName}, Qty={item.Quantity}",
                DetailDiscountPromotion = null,
                FinalDetailPrice = unitPrice * item.Quantity
            });
        }

        // 7. Apply promotions
        foreach (var group in groups)
        {
            var sellerDetails = order.OrderDetails
                .Where(d => d.SellerId == group.SellerId)
                .ToList();

            if (!group.PromotionId.HasValue)
            {
                foreach (var detail in sellerDetails) detail.FinalDetailPrice = detail.TotalPrice;
                continue;
            }

            var promotion = await _unitOfWork.Promotions.GetByIdAsync(group.PromotionId.Value);
            if (promotion == null || promotion.Status != PromotionStatus.Approved)
                throw ErrorHelper.BadRequest("Invalid promotion");

            if (promotion.StartDate > DateTime.UtcNow || promotion.EndDate < DateTime.UtcNow)
                throw ErrorHelper.BadRequest("Promotion is not active");

            var isParticipant = await _unitOfWork.PromotionParticipants
                .FirstOrDefaultAsync(p => p.PromotionId == promotion.Id && p.SellerId == group.SellerId);

            if (isParticipant == null)
                throw ErrorHelper.BadRequest("Seller not participating in this promotion");

            // Apply promotion to order details
            var subTotal = sellerDetails.Sum(d => d.TotalPrice);
            var discount = promotion.DiscountType == DiscountType.Percentage
                ? Math.Round(subTotal * promotion.DiscountValue / 100m, 2)
                : Math.Min(promotion.DiscountValue, subTotal);

            decimal totalAllocatedDiscount = 0;
            for (var i = 0; i < sellerDetails.Count; i++)
            {
                var detail = sellerDetails[i];
                decimal itemDiscount;

                if (i == sellerDetails.Count - 1)
                {
                    itemDiscount = discount - totalAllocatedDiscount;
                }
                else
                {
                    var ratio = detail.TotalPrice / subTotal;
                    itemDiscount = Math.Round(discount * ratio, 2);
                    totalAllocatedDiscount += itemDiscount;
                }

                itemDiscount = Math.Min(itemDiscount, detail.TotalPrice);
                detail.DetailDiscountPromotion = itemDiscount;
                detail.FinalDetailPrice = detail.TotalPrice - itemDiscount;
            }

            // Add promotion record
            order.OrderSellerPromotions.Add(new OrderSellerPromotion
            {
                OrderId = order.Id,
                SellerId = group.SellerId,
                PromotionId = promotion.Id,
                DiscountAmount = discount,
                Note = $"Applied {promotion.Code}"
            });
        }

        // 8. Calculate order totals
        order.TotalAmount = order.OrderDetails.Sum(d => d.TotalPrice);
        order.FinalAmount = order.OrderDetails.Sum(d => d.FinalDetailPrice);

        if (order.FinalAmount < 0)
            throw ErrorHelper.BadRequest("Final amount cannot be negative.");

        // 9. Save order
        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // 10. Process shipping
        if (shippingAddress != null)
        {
            order.TotalShippingFee = 0m;
            var physicalGroups = order.OrderDetails
                .Where(d => d.ProductId.HasValue)
                .GroupBy(d => d.SellerId)
                .ToList();

            foreach (var group in physicalGroups)
            {
                var sellerId = group.Key;
                if (!sellerCache.TryGetValue(sellerId, out var seller))
                    continue;

                var ghnResponse = await _ghnShippingService.PreviewOrderAsync(
                    _ghnShippingService.BuildGhnOrderRequest(
                        group.ToList(),
                        seller,
                        shippingAddress,
                        od => od.Product!,
                        od => od.Quantity
                    )
                );

                var shippingFee = ghnResponse?.TotalFee ?? 0m;
                order.TotalShippingFee += shippingFee;

                var shipment = new Shipment
                {
                    Provider = "GHN",
                    OrderCode = ghnResponse?.OrderCode ?? string.Empty,
                    TotalFee = (int)Math.Round(shippingFee),
                    TrackingNumber = ghnResponse?.OrderCode ?? string.Empty,
                    EstimatedDelivery = ghnResponse?.ExpectedDeliveryTime ?? DateTime.UtcNow.AddDays(3),
                    Status = ShipmentStatus.WAITING_PAYMENT
                };

                await _unitOfWork.Shipments.AddAsync(shipment);

                foreach (var detail in group)
                {
                    detail.Shipments.Add(shipment);
                    detail.Status = OrderDetailItemStatus.SHIPPING_REQUESTED;
                }
            }

            // Update order with shipping fees
            order.FinalAmount += order.TotalShippingFee;
            await _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();
        }

        // 11. Finalize checkout
        await _cacheService.RemoveByPatternAsync($"cart:{userId}:*");
        var sessionUrl = await _stripeService.CreateCheckoutSession(order.Id);
        await _cartItemService.UpdateCartAfterCheckoutAsync(userId, groups.SelectMany(g => g.Items).ToList());

        _loggerService.Success($"Checkout completed for order {order.Id}");
        return sessionUrl;
    }


    public struct CheckoutItem
    {
        public Guid SellerId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? BlindBoxId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
    }

    public class SellerCheckoutGroup
    {
        public Guid SellerId { get; set; }
        public Guid? PromotionId { get; set; }
        public List<CheckoutItem> Items { get; set; } = new();
    }
}