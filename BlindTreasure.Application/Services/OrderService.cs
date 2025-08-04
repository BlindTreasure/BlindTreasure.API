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

    private GhnOrderRequest BuildGhnOrderRequest<T>(
        IEnumerable<T> items,
        Seller seller,
        Address toAddress,
        Func<T, Product> getProduct,
        Func<T, int> getQuantity)
    {
        var ghnOrderItems = items.Select(item =>
        {
            var product = getProduct(item);
            var category = product.Category;
            var length = Convert.ToInt32(product.Length ?? 10);
            var width = Convert.ToInt32(product.Width ?? 10);
            var height = Convert.ToInt32(product.Height ?? 10);
            var weight = Convert.ToInt32(product.Weight ?? 1000);

            return new GhnOrderItemDto
            {
                Name = product.Name,
                Code = product.Id.ToString(),
                Quantity = getQuantity(item),
                Price = Convert.ToInt32(product.Price),
                Length = length,
                Width = width,
                Height = height,
                Weight = weight,
                Category = new GhnItemCategory
                {
                    Level1 = category?.Name,
                    Level2 = category?.Parent?.Name
                }
            };
        }).ToList();

        _loggerService.Info($"Build GHN order request for seller {seller.Id} with {ghnOrderItems.Count} items.");

        return new GhnOrderRequest
        {
            PaymentTypeId = 2,
            Note = $"Giao hàng cho seller {seller.CompanyName}",
            RequiredNote = "CHOXEMHANGKHONGTHU",
            FromName = seller.CompanyName ?? "BlindTreasure Warehouse",
            FromPhone = "0925136907" ?? seller.CompanyPhone,
            FromAddress = seller.CompanyAddress ?? "72 Thành Thái, Phường 14, Quận 10, Hồ Chí Minh, TP.HCM",
            FromWardName = seller.CompanyWardName ?? "Phường 14",
            FromDistrictName = seller.CompanyDistrictName ?? "Quận 10",
            FromProvinceName = seller.CompanyProvinceName ?? "HCM",
            ToName = toAddress.FullName,
            ToPhone = toAddress.Phone,
            ToAddress = toAddress.AddressLine,
            ToWardName = toAddress.Ward ?? "",
            ToDistrictName = toAddress.District ?? "",
            ToProvinceName = toAddress.Province,
            CodAmount = 0,
            Content = $"Giao hàng cho {toAddress.FullName} từ seller {seller.CompanyName}",
            Length = ghnOrderItems.Max(i => i.Length),
            Width = ghnOrderItems.Max(i => i.Width),
            Height = ghnOrderItems.Max(i => i.Height),
            Weight = ghnOrderItems.Sum(i => i.Weight),
            InsuranceValue = ghnOrderItems.Sum(i => i.Price * i.Quantity) <= 5000000
                ? ghnOrderItems.Sum(i => i.Price * i.Quantity)
                : 5000000,
            ServiceTypeId = 2,
            Items = ghnOrderItems.ToArray()
        };
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

        // 6. Build details and reserve stock
        foreach (var group in groups)
        {
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
                    Logs = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Created {itemName}, Qty={item.Quantity}"
                };
                order.OrderDetails.Add(detail);
            }

            // 7. Apply promotion
            if (group.PromotionId.HasValue)
            {
                var promo = await _unitOfWork.Promotions.GetByIdAsync(group.PromotionId.Value);
                if (promo == null || promo.Status != PromotionStatus.Approved)
                    throw ErrorHelper.BadRequest("Invalid promotion");
                var subTotal = order.OrderDetails.Where(d => d.SellerId == group.SellerId).Sum(d => d.TotalPrice);
                var discount = promo.DiscountType == DiscountType.Percentage
                    ? Math.Round(subTotal * promo.DiscountValue / 100m, 2)
                    : promo.DiscountValue;
                discount = Math.Min(discount, subTotal);
                order.OrderSellerPromotions.Add(new OrderSellerPromotion
                {
                    Order = order,
                    OrderId = order.Id,
                    SellerId = group.SellerId,
                    Promotion = promo,
                    DiscountAmount = discount,
                    Note = $"Applied {promo.Code}"
                });
                promo.UsageLimit = (promo.UsageLimit ?? 0) - 1;
                await _unitOfWork.Promotions.Update(promo);
            }
        }

        // 8. Totals before shipment
        order.TotalAmount = order.OrderDetails.Sum(d => d.TotalPrice);
        order.FinalAmount = order.TotalAmount - order.OrderSellerPromotions.Sum(p => p.DiscountAmount);
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
                    BuildGhnOrderRequest(grp, seller, shippingAddress, od => od.Product!, od => od.Quantity));
                var fee = ghnResp?.TotalFee ?? 0m;
                order.TotalShippingFee += fee;
                order.FinalAmount += fee;
                _loggerService.Info($"GHN shipment for seller {seller.Id}, fee={fee}");

                // Tạo 1 shipment cho cả group
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

                // Gắn shipment này vào tất cả các order detail vật lý của seller
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


    public async Task<List<ShipmentCheckoutResponseDTO>> PreviewShippingCheckoutAsync(List<DirectCartItemDto> items,
        bool? IsPreview = false)
    {
        _loggerService.Info("Preview shipping checkout started.");
        var userId = _claimsService.CurrentUserId;
        if (items == null || !items.Any())
            throw ErrorHelper.BadRequest("Cart trống.");

        // Chỉ lấy product vật lý để tính shipment
        var productItems = items.Where(i => i.ProductId.HasValue).ToList();
        if (!productItems.Any())
            throw ErrorHelper.BadRequest("Không có sản phẩm vật lý nào để tính phí vận chuyển.");

        var address = await _unitOfWork.Addresses.GetQueryable()
            .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
            .FirstOrDefaultAsync();
        if (address == null)
            throw ErrorHelper.BadRequest("Không tìm thấy địa chỉ mặc định của khách hàng.");

        var productIds = productItems.Select(i => i.ProductId.Value).ToList();
        var products = await _unitOfWork.Products.GetQueryable()
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .ToListAsync();

        var sellerGroups = items.GroupBy(i =>
        {
            var product = products.FirstOrDefault(p => p.Id == i.ProductId);
            return product?.SellerId;
        });

        var result = new List<ShipmentCheckoutResponseDTO>();

        foreach (var group in sellerGroups)
        {
            var sellerId = group.Key;
            var seller = products.FirstOrDefault(p => p.SellerId == sellerId)?.Seller;
            if (seller == null)
                continue;

            var groupItems = group.ToList();
            var itemsWithProduct = groupItems.Select(item => new
            {
                Product = products.First(p => p.Id == item.ProductId),
                Quantity = item.Quantity
            });

            var ghnOrderRequest = BuildGhnOrderRequest(
                itemsWithProduct,
                seller,
                address,
                x => x.Product,
                x => x.Quantity
            );

            var ghnPreviewResponse = await _ghnShippingService.PreviewOrderAsync(ghnOrderRequest);

            foreach (var item in groupItems)
                result.Add(new ShipmentCheckoutResponseDTO
                {
                    SellerId = seller.Id,
                    SellerCompanyName = seller.CompanyName,
                    Shipment = null,
                    GhnPreviewResponse = ghnPreviewResponse
                });
        }

        _loggerService.Info("Preview shipping checkout completed.");
        return result;
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

            var ghnOrderRequest = BuildGhnOrderRequest(
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