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

    public OrderService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IMapperService mapper,
        IProductService productService,
        IUnitOfWork unitOfWork,
        ICartItemService cartItemService,
        IStripeService stripeService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _mapper = mapper;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _cartItemService = cartItemService;
        _stripeService = stripeService;
    }

        /// <summary>
        /// Đặt hàng (checkout) từ giỏ hàng hệ thống, trả về link thanh toán Stripe.
        /// </summary>
        public async Task<string> CheckoutAsync(CreateOrderDto dto)
        {
            var cart = await _cartItemService.GetCurrentUserCartAsync();
            if (cart.Items == null || !cart.Items.Any())
            {
                _loggerService.Warn("[CheckoutAsync] Giỏ hàng trống.");
                throw ErrorHelper.BadRequest("Giỏ hàng trống.");
            }
            _loggerService.Info("[CheckoutAsync] Bắt đầu xử lý checkout từ cart hệ thống.");
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
                dto.ShippingAddressId
            );
        }

        /// <summary>
        /// Đặt hàng (checkout) từ cart truyền lên từ client, trả về link thanh toán Stripe.
        /// </summary>
        public async Task<string> CheckoutFromClientCartAsync(DirectCartCheckoutDto cartDto)
        {
            if (cartDto == null || cartDto.Items == null || !cartDto.Items.Any())
            {
                _loggerService.Warn("[CheckoutFromClientCartAsync] Cart truyền lên không hợp lệ hoặc trống.");
                throw ErrorHelper.BadRequest("Cart truyền lên không hợp lệ hoặc trống.");
            }
            _loggerService.Info("[CheckoutFromClientCartAsync] Bắt đầu xử lý checkout từ cart FE.");
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
                cartDto.ShippingAddressId
            );
        }

        /// <summary>
        /// Logic nghiệp vụ đặt hàng dùng chung cho mọi loại cart.
        /// Không tạo thêm DTO mới, chỉ dùng struct nội bộ.
        /// </summary>
        private async Task<string> CheckoutCore(
            IEnumerable<CheckoutItem> items,
            Guid? shippingAddressId)
        {
            var userId = _claimsService.CurrentUserId;
            var itemList = items.ToList();
            if (!itemList.Any())
            {
                _loggerService.Warn("[CheckoutCore] Giỏ hàng trống hoặc không hợp lệ.");
                throw ErrorHelper.BadRequest("Giỏ hàng trống hoặc không hợp lệ.");
            }

            // 1. Kiểm tra shipping address nếu có
            Address? shippingAddress = null;
            if (shippingAddressId.HasValue)
            {
                shippingAddress = await _unitOfWork.Addresses.GetByIdAsync(shippingAddressId.Value);
                if (shippingAddress == null || shippingAddress.IsDeleted || shippingAddress.UserId != userId)
                {
                    _loggerService.Warn($"[CheckoutCore] Địa chỉ giao hàng không hợp lệ hoặc không thuộc user.");
                    throw ErrorHelper.BadRequest("Địa chỉ giao hàng không hợp lệ hoặc không thuộc user.");
                }
            }

            // 1. Kiểm tra tồn kho & trạng thái sản phẩm/blindbox
            foreach (var item in itemList)
            {
                if (item.ProductId.HasValue)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId.Value);
                    if (product == null || product.IsDeleted)
                        throw ErrorHelper.NotFound($"Sản phẩm {item.ProductName} không tồn tại.");
                    if (product.Stock < item.Quantity)
                        throw ErrorHelper.BadRequest($"Sản phẩm {item.ProductName} không đủ tồn kho.");
                    if (product.Status != ProductStatus.Active)
                        throw ErrorHelper.BadRequest($"Sản phẩm {item.ProductName} không còn bán.");
                }
                else if (item.BlindBoxId.HasValue)
                {
                    var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(item.BlindBoxId.Value);
                    if (blindBox == null || blindBox.IsDeleted)
                        throw ErrorHelper.NotFound($"Blind box {item.BlindBoxName} không tồn tại.");
                    if (blindBox.Status != BlindBoxStatus.Approved)
                        throw ErrorHelper.BadRequest($"Blind box {item.BlindBoxName} chưa được duyệt.");
                    if (blindBox.TotalQuantity < item.Quantity)
                        throw ErrorHelper.BadRequest($"Blind box {item.BlindBoxName} không đủ số lượng.");
                }
            }

            // 2. Tính tổng tiền
            decimal totalPrice = itemList.Sum(i => i.TotalPrice);

            // 3. Tạo order
            var order = new Order
            {
                UserId = userId,
                Status = OrderStatus.PENDING.ToString(),
                TotalAmount = totalPrice,
                PlacedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                ShippingAddressId = shippingAddressId,
                OrderDetails = new List<OrderDetail>()
            };

            // 4. Tạo order details & trừ tồn kho
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

            // 5. cập nhật cart hệ thống 

            await _cartItemService.UpdateCartAfterCheckoutAsync(userId, itemList);
            _loggerService.Info("[CheckoutCore] Đã xóa giỏ hàng hệ thống sau khi đặt hàng.");


            // 6. Xóa cache liên quan
            await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
            _loggerService.Info($"[CheckoutCore] Đã xóa cache order:user:{userId}:* sau khi đặt hàng.");

            _loggerService.Success($"[CheckoutCore] Đặt hàng thành công cho user {userId}.");

            // 7. Gọi StripeService để lấy link thanh toán cho order vừa tạo
            return await _stripeService.CreateCheckoutSession(order.Id, false);
        }

        /// <summary>
        /// Lấy chi tiết đơn hàng của user hiện tại.
        /// </summary>
        public async Task<OrderDto> GetOrderByIdAsync(Guid orderId)
        {
            var userId = _claimsService.CurrentUserId;
            var cacheKey = $"order:user:{userId}:order:{orderId}";
            var cached = await _cacheService.GetAsync<OrderDto>(cacheKey);
            if (cached != null)
            {
                _loggerService.Info($"[GetOrderByIdAsync] Cache hit for order {orderId}");
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
            _loggerService.Warn($"[GetOrderByIdAsync] Đơn hàng {orderId} không tồn tại.");
            throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");
        }

            var dto = OrderDtoMapper.ToOrderDto(order);

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        _loggerService.Info($"[GetOrderByIdAsync] Order {orderId} loaded from DB and cached.");
        return dto;
    }

        /// <summary>
        /// Lấy danh sách đơn hàng của user hiện tại.
        /// </summary>
        public async Task<List<OrderDto>> GetMyOrdersAsync()
        {
            var userId = _claimsService.CurrentUserId;

            var orders = await _unitOfWork.Orders.GetQueryable().AsNoTracking()
                .Where(o => o.UserId == userId && !o.IsDeleted)
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

            _loggerService.Info("[GetMyOrdersAsync] User orders loaded from DB.");
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
            _loggerService.Warn($"[CancelOrderAsync] Đơn hàng {orderId} không tồn tại hoặc không thuộc user.");
            throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");
        }

        if (order.Status != OrderStatus.PENDING.ToString())
        {
            _loggerService.Warn($"[CancelOrderAsync] Đơn hàng {orderId} không ở trạng thái PENDING.");
            throw ErrorHelper.BadRequest("Chỉ có thể hủy đơn hàng ở trạng thái chờ xử lý.");
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
        _loggerService.Info($"[CancelOrderAsync] Đã xóa cache order:user:{userId}:* sau khi hủy đơn hàng.");

        _loggerService.Success($"[CancelOrderAsync] Đã hủy đơn hàng {orderId}.");
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
            _loggerService.Warn($"[DeleteOrderAsync] Đơn hàng {orderId} không tồn tại hoặc không thuộc user.");
            throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");
        }

        order.IsDeleted = true;
        order.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.Orders.Update(order);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache liên quan
        await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");
        _loggerService.Info($"[DeleteOrderAsync] Đã xóa cache order:user:{userId}:* sau khi xóa đơn hàng.");

        _loggerService.Success($"[DeleteOrderAsync] Đã xóa đơn hàng {orderId}.");
    }



        /// <summary>
        /// Struct nội bộ dùng chung cho logic checkout, không public ra ngoài.
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
}