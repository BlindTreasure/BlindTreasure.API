using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

/// <summary>
///     OrderService xử lý toàn bộ luồng đặt hàng, checkout, truy vấn đơn hàng.
///     Đã refactor để loại bỏ duplicate code, tách logic nghiệp vụ dùng chung, tuân thủ SOLID.
/// </summary>
public class OrderService : IOrderService
{
    private readonly ICacheService _cacheService;
    private readonly ICartItemService _cartItemService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapper;
    private readonly IProductService _productService;
    private readonly IStripeService _stripeService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPromotionService _promotionService;


    public OrderService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IMapperService mapper,
        IProductService productService,
        IUnitOfWork unitOfWork,
        ICartItemService cartItemService,
        IStripeService stripeService,
        IPromotionService promotionService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _mapper = mapper;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _cartItemService = cartItemService;
        _stripeService = stripeService;
        _promotionService = promotionService;
    }

    /// <summary>
    ///     Đặt hàng (checkout) từ giỏ hàng hệ thống, trả về link thanh toán Stripe.
    /// </summary>
    public async Task<string> CheckoutAsync(CreateCheckoutRequestDto dto)
    {
        var cart = await _cartItemService.GetCurrentUserCartAsync();
        if (cart.Items == null || !cart.Items.Any())
        {
            _loggerService.Warn(ErrorMessages.OrderCartEmptyLog);
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmpty);
        }

        _loggerService.Info(ErrorMessages.OrderCheckoutStartLog);
        return await CheckoutCore(
            cart.Items.Select(i => new CheckoutItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                BlindBoxId = i.BlindBoxId,
                BlindBoxName = i.BlindBoxName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }),
            dto.ShippingAddressId,
            dto.PromotionId
        );
    }

    /// <summary>
    ///     Đặt hàng (checkout) từ cart truyền lên từ client, trả về link thanh toán Stripe.
    /// </summary>
    public async Task<string> CheckoutFromClientCartAsync(DirectCartCheckoutDto cartDto)
    {
        if (cartDto == null || cartDto.Items == null || !cartDto.Items.Any())
        {
            _loggerService.Warn(ErrorMessages.OrderClientCartInvalidLog);
            throw ErrorHelper.BadRequest(ErrorMessages.OrderClientCartInvalid);
        }

        _loggerService.Info(ErrorMessages.OrderCheckoutFromClientStartLog);
        return await CheckoutCore(
            cartDto.Items.Select(i => new CheckoutItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                BlindBoxId = i.BlindBoxId,
                BlindBoxName = i.BlindBoxName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }),
            cartDto.ShippingAddressId,
            cartDto.PromotionId
        );
    }

    /// <summary>
    ///     Lấy chi tiết đơn hàng của user hiện tại.
    /// </summary>
    public async Task<OrderDto> GetOrderByIdAsync(Guid orderId)
    {
        var userId = _claimsService.CurrentUserId;
        var cacheKey = $"order:user:{userId}:order:{orderId}";
        var cached = await _cacheService.GetAsync<OrderDto>(cacheKey);
        if (cached != null)
        {
            _loggerService.Info(string.Format(ErrorMessages.OrderCacheHitLog, orderId));
            return cached;
        }

        var order = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.BlindBox)
            .Include(o => o.ShippingAddress)
            .Include(o => o.Payment)
            .ThenInclude(p => p.Transactions)
            .OrderByDescending(o => o.PlacedAt)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            _loggerService.Warn(string.Format(ErrorMessages.OrderNotFoundLog, orderId));
            throw ErrorHelper.NotFound(ErrorMessages.OrderNotFound);
        }

        var dto = OrderDtoMapper.ToOrderDto(order);

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        _loggerService.Info(string.Format(ErrorMessages.OrderLoadedAndCachedLog, orderId));
        return dto;
    }

    /// <summary>
    ///     Lấy danh sách đơn hàng của user hiện tại.
    /// </summary>
    public async Task<List<OrderDto>> GetMyOrdersAsync()
    {
        var userId = _claimsService.CurrentUserId;

        var orders = await _unitOfWork.Orders.GetQueryable().AsNoTracking()
            .Where(o => o.UserId == userId && !o.IsDeleted)
            .Include(o => o.Promotion)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.BlindBox)
            .Include(o => o.ShippingAddress)
            .Include(o => o.Payment)
            .ThenInclude(p => p.Transactions)
            .OrderByDescending(o => o.PlacedAt)
            .ToListAsync();

        var dtos = orders.Select(OrderDtoMapper.ToOrderDto).ToList();

        _loggerService.Info(ErrorMessages.OrderListLoadedLog);
        return dtos;
    }

    /// <summary>
    ///     Hủy đơn hàng (chỉ khi trạng thái cho phép), trả lại tồn kho.
    /// </summary>
    public async Task CancelOrderAsync(Guid orderId)
    {
        var userId = _claimsService.CurrentUserId;
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, o => o.OrderDetails);
        if (order == null || order.IsDeleted || order.UserId != userId)
        {
            _loggerService.Warn(string.Format(ErrorMessages.OrderNotFoundOrNotBelongToUserLog, orderId));
            throw ErrorHelper.NotFound(ErrorMessages.OrderNotFound);
        }

        if (order.Status != OrderStatus.PENDING.ToString())
        {
            _loggerService.Warn(string.Format(ErrorMessages.OrderNotPendingLog, orderId));
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCancelOnlyPending);
        }

        order.Status = OrderStatus.CANCELLED.ToString();
        order.UpdatedAt = DateTime.UtcNow;

        // Trả lại tồn kho nếu là product hoặc blindbox
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

        // Xóa cache liên quan
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Info(string.Format(ErrorMessages.OrderCacheClearedAfterCancelLog, userId));

        _loggerService.Success(string.Format(ErrorMessages.OrderCancelSuccessLog, orderId));
    }

    /// <summary>
    ///     Xóa mềm đơn hàng (chỉ cho phép user xóa đơn của mình).
    /// </summary>
    public async Task DeleteOrderAsync(Guid orderId)
    {
        var userId = _claimsService.CurrentUserId;
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null || order.IsDeleted || order.UserId != userId)
        {
            _loggerService.Warn($"[DeleteOrderAsync] Order {orderId} not found or does not belong to user.");
            throw ErrorHelper.NotFound(ErrorMessages.OrderNotFound);
        }

        order.IsDeleted = true;
        order.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.Orders.Update(order);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache liên quan
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Info(string.Format(ErrorMessages.OrderCacheClearedAfterDeleteLog, userId));

        _loggerService.Success(string.Format(ErrorMessages.OrderDeleteSuccessLog, orderId));
    }

    /// <summary>
    ///     Logic nghiệp vụ đặt hàng dùng chung cho mọi loại cart.
    ///     Không tạo thêm DTO mới, chỉ dùng struct nội bộ.
    /// </summary>
    private async Task<string> CheckoutCore(
        IEnumerable<CheckoutItem> items,
        Guid? shippingAddressId,
        Guid? promotionId = null)
    {
        var userId = _claimsService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _loggerService.Warn(ErrorMessages.AccountNotFound);
            throw ErrorHelper.Forbidden(ErrorMessages.AccountNotFound);
        }
        var itemList = items.ToList();
        if (!itemList.Any())
        {
            _loggerService.Warn(ErrorMessages.OrderCartEmptyOrInvalidLog);
            throw ErrorHelper.BadRequest(ErrorMessages.OrderCartEmptyOrInvalid);
        }

        // 1. Kiểm tra shipping address nếu có
        Address? shippingAddress = null;
        if (shippingAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses.GetByIdAsync(shippingAddressId.Value);
            if (shippingAddress == null || shippingAddress.IsDeleted || shippingAddress.UserId != userId)
            {
                _loggerService.Warn(ErrorMessages.OrderShippingAddressInvalidLog);
                throw ErrorHelper.BadRequest(ErrorMessages.OrderShippingAddressInvalid);
            }
        }

        // 1. Kiểm tra tồn kho & trạng thái sản phẩm/blindbox
        foreach (var item in itemList)
            if (item.ProductId.HasValue)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId.Value);
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
                    throw ErrorHelper.BadRequest(string.Format(ErrorMessages.OrderBlindBoxNotApproved,
                        item.BlindBoxName));
                if (blindBox.TotalQuantity < item.Quantity)
                    throw ErrorHelper.BadRequest(
                        string.Format(ErrorMessages.OrderBlindBoxOutOfStock, item.BlindBoxName));
            }

        // 2. Tính tổng tiền
        var totalPrice = itemList.Sum(i => i.TotalPrice);

        // 3. Áp dụng promotion nếu có
        decimal discountAmount = 0;
        string? promotionNote = null;
        Promotion? promotion = null;

        if (promotionId.HasValue)
        {
            promotion = await _unitOfWork.Promotions.GetByIdAsync(promotionId.Value);
            if (promotion == null)
                throw ErrorHelper.BadRequest("Voucher không tồn tại.");

            if (promotion.Status != PromotionStatus.Approved)
                throw ErrorHelper.BadRequest("Voucher chưa được duyệt.");

            var now = DateTime.UtcNow;
            if (now < promotion.StartDate || now > promotion.EndDate)
                throw ErrorHelper.BadRequest("Voucher đã hết hạn hoặc chưa bắt đầu.");

            if (promotion.DiscountType == DiscountType.Percentage)
                discountAmount = Math.Round(totalPrice * (promotion.DiscountValue / 100m), 2);
            else if (promotion.DiscountType == DiscountType.Fixed)
                discountAmount = promotion.DiscountValue;

            discountAmount = Math.Min(discountAmount, totalPrice);
            promotionNote = $"Áp dụng voucher {promotion.Code}, giảm {discountAmount:N0}đ";

        }

        // 5. Tạo order
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.PENDING.ToString(),
            TotalAmount = totalPrice,
            FinalAmount = totalPrice - discountAmount,
            PlacedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ShippingAddressId = shippingAddressId,
            PromotionId = promotionId,
            Promotion = promotion,
            DiscountAmount = discountAmount > 0 ? discountAmount : null,
            PromotionNote = promotionNote,
            OrderDetails = new List<OrderDetail>()
        };

        // 6. Tạo order details & trừ tồn kho
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
                Status = OrderDetailStatus.PENDING.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            order.OrderDetails.Add(orderDetail);

            if (item.ProductId.HasValue)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId.Value);
                product.Stock -= item.Quantity;
                await _unitOfWork.Products.Update(product);
            }
            else if (item.BlindBoxId.HasValue)
            {
                var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(item.BlindBoxId.Value);
                blindBox.TotalQuantity -= item.Quantity;
                await _unitOfWork.BlindBoxes.Update(blindBox);
            }
        }

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // 7. cập nhật cart hệ thống 
        await _cartItemService.UpdateCartAfterCheckoutAsync(userId, itemList);
        _loggerService.Info(ErrorMessages.OrderCartClearedAfterCheckoutLog);

        // 8. Xóa cache liên quan
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Info(string.Format(ErrorMessages.OrderCacheClearedAfterCheckoutLog, userId));

        _loggerService.Success(string.Format(ErrorMessages.OrderCheckoutSuccessLog, userId));

        // 9. Gọi StripeService để lấy link thanh toán cho order vừa tạo
        return await _stripeService.CreateCheckoutSession(order.Id);
    }

    /// <summary>
    ///     Struct nội bộ dùng chung cho logic checkout, không public ra ngoài.
    /// </summary>
    public struct CheckoutItem
    {
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public Guid? BlindBoxId { get; set; }
        public string? BlindBoxName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}