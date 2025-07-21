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

        Guid? shippingAddressId = null;
        if (dto.IsShip == true)
        {
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

        Guid? shippingAddressId = null;
        if (cartDto.IsShip == true)
        {
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
                PromotionId = i.PromotionId
            }),
            shippingAddressId
        );
        _loggerService.Success("Checkout from client cart completed.");
        return result;
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
        List<Order> orders = param.PageIndex == 0
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
            od.Status = OrderDetailStatus.CANCELLED.ToString();
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
            int length = Convert.ToInt32(product.Length ?? 10);
            int width = Convert.ToInt32(product.Width ?? 10);
            int height = Convert.ToInt32(product.Height ?? 10);
            int weight = Convert.ToInt32(product.Weight ?? 1000);

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
                    Level2 = category?.Parent?.Name,
                    Level3 = category?.Parent?.Parent?.Name
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
            FromPhone = "0925136907" ?? seller.CompanyPhone  ,
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

    private async Task<string> CheckoutCore(IEnumerable<CheckoutItem> items, Guid? shippingAddressId)
    {
        _loggerService.Info("Start core checkout logic.");
        var userId = _claimsService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.Forbidden(ErrorMessages.AccountNotFound);

        var itemList = items.ToList();
        if (!itemList.Any())
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmptyOrInvalid);

        Address shippingAddress = null;
        if (shippingAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses.GetByIdAsync(shippingAddressId.Value);
            if (shippingAddress == null || shippingAddress.IsDeleted || shippingAddress.UserId != userId)
                throw ErrorHelper.BadRequest(ErrorMessages.OrderShippingAddressInvalid);
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
        {
            if (item.ProductId.HasValue)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product == null || product.IsDeleted)
                    throw ErrorHelper.NotFound(string.Format(ErrorMessages.OrderProductNotFound, item.ProductName));
                if (product.Stock < item.Quantity)
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderProductOutOfStock, item.ProductName));
                if (product.Status != ProductStatus.Active)
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderProductNotForSale, item.ProductName));
            }
            else if (item.BlindBoxId.HasValue)
            {
                var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(item.BlindBoxId.Value);
                if (blindBox == null || blindBox.IsDeleted)
                    throw ErrorHelper.NotFound(string.Format(ErrorMessages.OrderBlindBoxNotFound, item.BlindBoxName));
                if (blindBox.Status != BlindBoxStatus.Approved)
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderBlindBoxNotApproved, item.BlindBoxName));
                if (blindBox.TotalQuantity < item.Quantity)
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderBlindBoxOutOfStock, item.BlindBoxName));
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

        foreach (var item in itemList)
        {
            var sellerId = products.First(p => p.Id == item.ProductId).SellerId;
            var od = new OrderDetail
            {
                Id = Guid.NewGuid(),
                Order = order,
                ProductId = item.ProductId,
                BlindBoxId = item.BlindBoxId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                SellerId = sellerId,
                Status = OrderDetailStatus.PENDING.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            order.OrderDetails.Add(od);

            if (item.ProductId.HasValue)
            {
                var product = products.First(p => p.Id == item.ProductId);
                product.Stock -= item.Quantity;
                await _unitOfWork.Products.Update(product);
            }
            else if (item.BlindBoxId.HasValue)
            {
                var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(item.BlindBoxId.Value);
                blindBox.TotalQuantity -= item.Quantity;
                if (blindBox.TotalQuantity <= 0 && blindBox.Status == BlindBoxStatus.Approved)
                    blindBox.Status = BlindBoxStatus.Rejected;
                await _unitOfWork.BlindBoxes.Update(blindBox);
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

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        if (shippingAddress != null)
        {
            var orderDetailIds = order.OrderDetails.Select(od => od.Id).ToList();
            var orderDetailsWithProduct = await _unitOfWork.OrderDetails.GetQueryable()
                .Where(od => orderDetailIds.Contains(od.Id))
                .Include(od => od.Product).ThenInclude(p => p.Category)
                .Include(od => od.Product).ThenInclude(p => p.Seller)
                .ToListAsync();

            var sellerGroups = orderDetailsWithProduct.GroupBy(od => od.Product.SellerId);

            foreach (var group in sellerGroups)
            {
                var seller = group.First().Product.Seller;
                var ghnRequest = BuildGhnOrderRequest(group, seller, shippingAddress, od => od.Product, od => od.Quantity);
                var ghnResponse = await _ghnShippingService.PreviewOrderAsync(ghnRequest);

                order.TotalAmount += ghnResponse?.TotalFee ?? 0;
                foreach (var od in group)
                {
                    var shipment = new Shipment
                    {
                        OrderDetailId = od.Id,
                        Provider = "GHN",
                        OrderCode = ghnResponse?.OrderCode,
                        TotalFee = ghnResponse?.TotalFee != null ? Convert.ToInt32(ghnResponse.TotalFee.Value) : 0,
                        MainServiceFee = (int)(ghnResponse?.Fee?.MainService ?? 0),
                        TrackingNumber = ghnResponse?.OrderCode ?? string.Empty,
                        ShippedAt = DateTime.UtcNow,
                        EstimatedDelivery = ghnResponse?.ExpectedDeliveryTime ?? DateTime.UtcNow.AddDays(3),
                        Status = "WAITING_PAYMENT"
                    };
                    await _unitOfWork.Shipments.AddAsync(shipment);
                    od.Status = OrderDetailStatus.DELIVERING.ToString();
                    await _unitOfWork.OrderDetails.Update(od);
                }
            }
            await _unitOfWork.SaveChangesAsync();
            order.FinalAmount = order.TotalAmount - order.OrderSellerPromotions.Sum(osp => osp.DiscountAmount);
            await _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();
        }

        await _cartItemService.UpdateCartAfterCheckoutAsync(userId, itemList);
        return await _stripeService.CreateCheckoutSession(order.Id);
    }


    public async Task<List<ShipmentCheckoutResponseDTO>> PreviewShippingCheckoutAsync(List<DirectCartItemDto> items, bool? IsPreview = false)
    {
        _loggerService.Info("Preview shipping checkout started.");
        var userId = _claimsService.CurrentUserId;
        if (items == null || !items.Any())
            throw ErrorHelper.BadRequest("Cart trống.");

        var address = await _unitOfWork.Addresses.GetQueryable()
            .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
            .FirstOrDefaultAsync();
        if (address == null)
            throw ErrorHelper.BadRequest("Không tìm thấy địa chỉ mặc định của khách hàng.");

        var productIds = items.Select(i => i.ProductId.Value).ToList();
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
            {
                result.Add(new ShipmentCheckoutResponseDTO
                {
                    SellerId = seller.Id,
                    SellerCompanyName = seller.CompanyName,
                    Shipment = null,
                    GhnPreviewResponse = ghnPreviewResponse
                });
            }
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
        public Guid? PromotionId { get; set; } // voucher được apply theo seller nên phải tính trên item
    }
}