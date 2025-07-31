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
        if (cart.Items == null || !cart.Items.Any())
        {
            _loggerService.Warn("Cart is empty.");
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmpty);
        }

        // Lọc product vật lý
        var hasProduct = cart.Items.Any(i => i.ProductId.HasValue);
        var hasBlindBox = cart.Items.Any(i => i.BlindBoxId.HasValue);

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

        var result = await CheckoutCore(
            cart.Items.Select(i => new CheckoutItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                BlindBoxId = i.BlindBoxId,
                BlindBoxName = i.BlindBoxName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,

            }),
            shippingAddressId
 
        );
        _loggerService.Success("Checkout from system cart completed.");
        return result;
    }

    public async Task<string> CheckoutFromClientCartAsync(DirectCartCheckoutDto cartDto)
    {
        _loggerService.Info("Start checkout from client cart.");
        if (cartDto == null || cartDto.Items == null || !cartDto.Items.Any())
        {
            _loggerService.Warn("Client cart is invalid or empty.");
            throw ErrorHelper.BadRequest(ErrorMessages.OrderClientCartInvalid);
        }

        // Lọc product vật lý
        var hasProduct = cartDto.Items.Any(i => i.ProductId.HasValue);
        var hasBlindBox = cartDto.Items.Any(i => i.BlindBoxId.HasValue);

        Guid? shippingAddressId = null;
        if (cartDto.IsShip == true)
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

        var result = await CheckoutCore(
            cartDto.Items.Select(i => new CheckoutItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                BlindBoxId = i.BlindBoxId,
                BlindBoxName = i.BlindBoxName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                PromotionId = i.PromotionId // Thêm PromotionId nếu có
            }),
            shippingAddressId

        );
        _loggerService.Success("Checkout from client cart completed.");
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

        var dtos = orderDetails.Select(OrderDtoMapper.ToOrderDetailDto).ToList();
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
        //    .Include(o => o.ShippingAddress)
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
            InsuranceValue = ghnOrderItems.Sum(i => i.Price * i.Quantity),
            ServiceTypeId = 2,
            Items = ghnOrderItems.ToArray()
        };
    }

    private async Task<string> CheckoutCore(
        IEnumerable<CheckoutItem> items,
        Guid? shippingAddressId)
    {
        _loggerService.Info("Start core checkout logic.");
        var userId = _claimsService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _loggerService.Warn("User not found or deleted.");
            throw ErrorHelper.Forbidden(ErrorMessages.AccountNotFound);
        }

        var itemList = items.ToList();
        if (!itemList.Any())
        {
            _loggerService.Warn("Cart is empty or invalid.");
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmptyOrInvalid);
        }

        // Nếu có yêu cầu ship nhưng không có product vật lý nào
        if (shippingAddressId.HasValue && !itemList.Any(i => i.ProductId.HasValue))
        {
            _loggerService.Warn("Cart only contains BlindBox, cannot ship.");
            throw ErrorHelper.BadRequest("Không thể giao hàng: Giỏ hàng không có sản phẩm vật lý nào để ship.");
        }

        Address? shippingAddress = null;
        if (shippingAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses.GetByIdAsync(shippingAddressId.Value);
            if (shippingAddress == null || shippingAddress.IsDeleted || shippingAddress.UserId != userId)
            {
                _loggerService.Warn("Shipping address invalid.");
                throw ErrorHelper.BadRequest(ErrorMessages.OrderShippingAddressInvalid);
            }
        }

        var productIds = itemList.Where(i => i.ProductId.HasValue)
                              .Select(i => i.ProductId.Value)
                              .Distinct()
                              .ToList();
        var products = await _unitOfWork.Products.GetQueryable()
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Seller)
            .ToListAsync();

        foreach (var item in itemList)
            if (item.ProductId.HasValue)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId.Value);
                if (product == null || product.IsDeleted)
                {
                    _loggerService.Warn($"Product {item.ProductId} not found or deleted.");
                    throw ErrorHelper.NotFound(string.Format(ErrorMessages.OrderProductNotFound, item.ProductName));
                }

                if (product.Stock < item.Quantity)
                {
                    _loggerService.Warn($"Product {item.ProductId} out of stock.");
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderProductOutOfStock, item.ProductName));
                }

                if (product.Status != ProductStatus.Active)
                {
                    _loggerService.Warn($"Product {item.ProductId} not for sale.");
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderProductNotForSale, item.ProductName));
                }
            }
            else if (item.BlindBoxId.HasValue)
            {
                var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(item.BlindBoxId.Value);
                if (blindBox == null || blindBox.IsDeleted)
                {
                    _loggerService.Warn($"BlindBox {item.BlindBoxId} not found or deleted.");
                    throw ErrorHelper.NotFound(string.Format(ErrorMessages.OrderBlindBoxNotFound, item.BlindBoxName));
                }

                if (blindBox.Status != BlindBoxStatus.Approved)
                {
                    _loggerService.Warn($"BlindBox {item.BlindBoxId} not approved.");
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderBlindBoxNotApproved,
                        item.BlindBoxName));
                }

                if (blindBox.TotalQuantity < item.Quantity)
                {
                    _loggerService.Warn($"BlindBox {item.BlindBoxId} out of stock.");
                    throw ErrorHelper.BadRequest(
                        string.Format(ErrorMessages.OrderBlindBoxOutOfStock, item.BlindBoxName));
                }
            }

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

        var orderDetails = new List<OrderDetail>();
        foreach (var item in itemList)
        {
            var orderDetail = new OrderDetail
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = item.ProductId,
                BlindBoxId = item.BlindBoxId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                Status = OrderDetailItemStatus.PENDING,
                CreatedAt = DateTime.UtcNow
            };

            if( orderDetail.TotalPrice != (item.Quantity * item.UnitPrice))
            {
                _loggerService.Warn($"Total price mismatch for item {item.ProductName ?? item.BlindBoxName}.");
                throw ErrorHelper.BadRequest("Tổng tiền không khớp với số lượng và đơn giá.");
            }
            order.OrderDetails.Add(orderDetail);
            orderDetails.Add(orderDetail);

            if (item.ProductId.HasValue)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId.Value, x => x.Seller);
                product.Stock -= item.Quantity;
                await _unitOfWork.Products.Update(product);
                orderDetail.SellerId = product.SellerId;
            }
            else if (item.BlindBoxId.HasValue)
            {
                var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(item.BlindBoxId.Value, x => x.Seller);
                blindBox.TotalQuantity -= item.Quantity;
                if (blindBox.TotalQuantity <= 0 && blindBox.Status == BlindBoxStatus.Approved)
                {
                    blindBox.Status = BlindBoxStatus.Rejected;
                    _loggerService.Info($"BlindBox {blindBox.Id} is now out of stock and set to Rejected.");
                }

                await _unitOfWork.BlindBoxes.Update(blindBox);
                orderDetail.SellerId = blindBox.SellerId;
            }
        }

        var sellerPromos = itemList
         .Where(i => i.PromotionId.HasValue)
         .GroupBy(i => new { Seller = products.First(p => p.Id == i.ProductId).SellerId, Promo = i.PromotionId.Value })
         .Select(g => new { g.Key.Seller, g.Key.Promo })
         .ToList();

        foreach (var sp in sellerPromos)
        {
            var promo = await _unitOfWork.Promotions.GetByIdAsync(sp.Promo);
            if (promo == null || promo.Status != PromotionStatus.Approved)
                throw ErrorHelper.BadRequest("Invalid promotion");

            var subTotal = order.OrderDetails
                .Where(od => od.SellerId == sp.Seller)
                .Sum(od => od.TotalPrice);

            decimal discount = promo.DiscountType == DiscountType.Percentage
                ? Math.Round(subTotal * promo.DiscountValue / 100m, 2)
                : promo.DiscountValue;
            discount = Math.Min(discount, subTotal);

            var osp = new OrderSellerPromotion
            {
                Order = order,
                SellerId = sp.Seller,
                Promotion = promo,
                DiscountAmount = discount,
                Note = $"Applied {promo.Code}, -{discount:N0}"
            };
            order.OrderSellerPromotions.Add(osp);

            promo.UsageLimit = (promo.UsageLimit ?? 0) - 1;
            await _unitOfWork.Promotions.Update(promo);
        }

        order.TotalAmount = order.OrderDetails.Sum(od => od.TotalPrice);
        order.FinalAmount = order.TotalAmount - order.OrderSellerPromotions.Sum(osp => osp.DiscountAmount);
        if (order.FinalAmount < 0)
        {
            _loggerService.Warn("Final amount cannot be negative, resetting to zero.");
            throw ErrorHelper.BadRequest("Tổng tiền không thể âm, vui lòng kiểm tra lại code.");
        }

        order = await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        if (shippingAddress != null)
        {
            var orderDetailIds = orderDetails.Select(od => od.Id).ToList();
            var orderDetailsWithProduct = await _unitOfWork.OrderDetails.GetQueryable()
                .Where(od => orderDetailIds.Contains(od.Id))
                .Include(od => od.Product).ThenInclude(p => p.Category)
                .Include(od => od.Product).ThenInclude(p => p.Seller)
                .ToListAsync();

            var sellerGroups = orderDetailsWithProduct
                .Where(od => od.ProductId.HasValue && od.Product != null)
                .GroupBy(od => od.Product.SellerId);

            foreach (var group in sellerGroups)
            {
                var seller = group.First().Product.Seller;
                if (seller == null) continue;

                var ghnOrderRequest = BuildGhnOrderRequest(
                    group,
                    seller,
                    shippingAddress,
                    od => od.Product,
                    od => od.Quantity
                );

                var ghnCreateResponse =
                    await _ghnShippingService
                        .PreviewOrderAsync(
                            ghnOrderRequest); // sửa thành chính thức sang preview vì đây là tạo yêu cầu thanh toán

                order.TotalAmount += ghnCreateResponse?.TotalFee ?? 0;
                _loggerService.Info(
                    $"Created GHN shipment for seller {seller.Id}, fee: {ghnCreateResponse?.TotalFee ?? 0}");

                foreach (var od in group)
                {
                    var shipment = new Shipment
                    {
                        OrderDetailId = od.Id,
                        Provider = "GHN",
                        OrderCode = ghnCreateResponse?.OrderCode,
                        TotalFee = ghnCreateResponse?.TotalFee != null
                            ? Convert.ToInt32(ghnCreateResponse.TotalFee.Value)
                            : 0,
                        MainServiceFee = (int)(ghnCreateResponse?.Fee?.MainService ?? 0),
                        TrackingNumber = ghnCreateResponse?.OrderCode ?? "",
                        ShippedAt = DateTime.UtcNow,
                        EstimatedDelivery = ghnCreateResponse?.ExpectedDeliveryTime != default
                            ? ghnCreateResponse.ExpectedDeliveryTime
                            : DateTime.UtcNow.AddDays(3),
                        Status = ShipmentStatus.WAITING_PAYMENT // chưa thanh toán, chờ xác nhận
                    };
                    await _unitOfWork.Shipments.AddAsync(shipment);
                    order.FinalAmount = order.TotalAmount - order.OrderSellerPromotions.Sum(osp => osp.DiscountAmount);
                    od.Status = OrderDetailItemStatus.SHIPPING_REQUESTED;
                    od.Shipments.Add(shipment);
                    _loggerService.Info($"Shipment created for OrderDetail {od.Id} with GHN.");

                    await _unitOfWork.OrderDetails.Update(od);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        await _cartItemService.UpdateCartAfterCheckoutAsync(userId, itemList);
        _loggerService.Info("Cart updated after checkout.");
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Info("Order cache cleared after checkout.");
        _loggerService.Success($"Order checkout success for user {userId}.");
        return await _stripeService.CreateCheckoutSession(order.Id);
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

    public struct CheckoutItem
    {
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public Guid? BlindBoxId { get; set; }
        public string? BlindBoxName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public Guid? SellerId { get; set; } // Thêm SellerId nếu cần
        public Guid? PromotionId { get; set; } // Thêm PromotionId nếu cần
    }
}