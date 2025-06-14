using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class OrderService : IOrderService
{
    private readonly ICacheService _cacheService;
    private readonly ICartItemService _cartItemService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapper;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(ICacheService cacheService, IClaimsService claimsService, ILoggerService loggerService,
        IMapperService mapper, IProductService productService, IUnitOfWork unitOfWork, ICartItemService cartItemService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _mapper = mapper;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _cartItemService = cartItemService;
    }

    /// <summary>
    ///     Đặt hàng (checkout) từ giỏ hàng, tạo order và order details, kiểm tra tồn kho, trừ kho, xóa cart.
    /// </summary>
    public async Task<OrderDto> CheckoutAsync(CreateOrderDto dto)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var cart = await _cartItemService.GetCurrentUserCartAsync();
            if (cart.Items == null || !cart.Items.Any())
                throw ErrorHelper.BadRequest("Giỏ hàng trống.");

            //// Kiểm tra địa chỉ giao hàng
            //Address? address = null;
            //if (dto.ShippingAddressId != null)
            //{
            //    address = await _unitOfWork.Addresses.GetByIdAsync(dto.ShippingAddressId.Value);
            //    if (address == null || address.UserId != userId)
            //        throw ErrorHelper.BadRequest("Địa chỉ giao hàng không hợp lệ.");
            //}

            // Kiểm tra tồn kho và trạng thái sản phẩm/blindbox
            foreach (var item in cart.Items)
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

            // Tạo order
            var order = new Order
            {
                UserId = userId,
                Status = OrderStatus.PENDING.ToString(),
                TotalAmount = cart.TotalPrice,
                PlacedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                ShippingAddressId = dto.ShippingAddressId,
                OrderDetails = new List<OrderDetail>()
            };

            // Tạo order details và trừ tồn kho
            foreach (var item in cart.Items)
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

                // Trừ tồn kho nếu là product
                if (item.ProductId.HasValue)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId.Value);
                    product.Stock -= item.Quantity;
                    await _unitOfWork.Products.Update(product);
                }
                // Trừ số lượng blindbox
                else if (item.BlindBoxId.HasValue)
                {
                    var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(item.BlindBoxId.Value);
                    blindBox.TotalQuantity -= item.Quantity;
                    await _unitOfWork.BlindBoxes.Update(blindBox);
                }
            }

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            // Xóa giỏ hàng sau khi đặt hàng thành công
            await _cartItemService.ClearCartAsync();

            // Xóa cache liên quan nếu có
            await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");

            _loggerService.Success($"[CheckoutAsync] Đặt hàng thành công cho user {userId}.");
            return await GetOrderByIdAsync(order.Id);
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[CheckoutAsync] {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Lấy chi tiết đơn hàng của user hiện tại.
    /// </summary>
    public async Task<OrderDto> GetOrderByIdAsync(Guid orderId)
    {
        try
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
                .FirstOrDefaultAsync();

            if (order == null)
                throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");

            var dto = ToOrderDto(order);

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
            _loggerService.Info($"[GetOrderByIdAsync] Order {orderId} loaded from DB and cached.");
            return dto;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[GetOrderByIdAsync] {ex.Message}");
            throw;
        }
    }

    // <summary>
    /// Lấy danh sách đơn hàng của user hiện tại.
    /// </summary>
    public async Task<List<OrderDto>> GetMyOrdersAsync()
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var cacheKey = $"order:user:{userId}:all";
            var cached = await _cacheService.GetAsync<List<OrderDto>>(cacheKey);
            if (cached != null)
            {
                _loggerService.Info("[GetMyOrdersAsync] Cache hit for user orders.");
                return cached;
            }

            var orders = await _unitOfWork.Orders.GetQueryable()
                .Where(o => o.UserId == userId && !o.IsDeleted)
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.BlindBox)
                .Include(o => o.ShippingAddress)
                .OrderByDescending(o => o.PlacedAt)
                .ToListAsync();

            var dtos = orders.Select(ToOrderDto).ToList();

            await _cacheService.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(10));
            _loggerService.Info("[GetMyOrdersAsync] User orders loaded from DB and cached.");
            return dtos;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[GetMyOrdersAsync] {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Hủy đơn hàng (chỉ khi trạng thái cho phép), trả lại tồn kho.
    /// </summary>
    public async Task CancelOrderAsync(Guid orderId)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId, o => o.OrderDetails);
            if (order == null || order.IsDeleted || order.UserId != userId)
                throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");

            if (order.Status != OrderStatus.PENDING.ToString())
                throw ErrorHelper.BadRequest("Chỉ có thể hủy đơn hàng ở trạng thái chờ xử lý.");

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

            _loggerService.Success($"[CancelOrderAsync] Đã hủy đơn hàng {orderId}.");
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[CancelOrderAsync] {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Xóa mềm đơn hàng (chỉ cho phép user xóa đơn của mình).
    /// </summary>
    public async Task DeleteOrderAsync(Guid orderId)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null || order.IsDeleted || order.UserId != userId)
                throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");

            order.IsDeleted = true;
            order.DeletedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();

            // Xóa cache liên quan
            await _cacheService.RemoveByPatternAsync($"order:user:{userId}:*");

            _loggerService.Success($"[DeleteOrderAsync] Đã xóa đơn hàng {orderId}.");
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[DeleteOrderAsync] {ex.Message}");
            throw;
        }
    }


    /// <summary>
    ///     Mapping Order entity sang DTO.
    /// </summary>
    private static OrderDto ToOrderDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            PlacedAt = order.PlacedAt,
            CompletedAt = order.CompletedAt,
            ShippingAddress = order.ShippingAddress != null
                ? new OrderAddressDto
                {
                    Id = order.ShippingAddress.Id,
                    FullName = order.ShippingAddress.FullName,
                    Phone = order.ShippingAddress.Phone,
                    AddressLine1 = order.ShippingAddress.AddressLine1,
                    AddressLine2 = order.ShippingAddress.AddressLine2,
                    City = order.ShippingAddress.City,
                    Province = order.ShippingAddress.Province,
                    PostalCode = order.ShippingAddress.PostalCode,
                    Country = order.ShippingAddress.Country
                }
                : null,
            Details = order.OrderDetails?.Select(od => new OrderDetailDto
            {
                Id = od.Id,
                ProductId = od.ProductId,
                ProductName = od.Product?.Name,
                ProductImages = od.Product?.ImageUrls,
                BlindBoxId = od.BlindBoxId,
                BlindBoxName = od.BlindBox?.Name,
                BlindBoxImage = od.BlindBox?.ImageUrl,
                Quantity = od.Quantity,
                UnitPrice = od.UnitPrice,
                TotalPrice = od.TotalPrice,
                Status = od.Status,
                ShippedAt = od.ShippedAt,
                ReceivedAt = od.ReceivedAt
            }).ToList() ?? new List<OrderDetailDto>()
        };
    }
}