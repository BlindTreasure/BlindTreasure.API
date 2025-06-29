using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IOrderService _orderService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryItemService _inventoryItemService;
    private readonly ICustomerInventoryService _customerInventoryService;

    public TransactionService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService logger,
        IMapperService mapper,
        IOrderService orderService,
        IUnitOfWork unitOfWork,
        IInventoryItemService inventoryItemService,
        ICustomerInventoryService customerInventoryService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _orderService = orderService;
        _unitOfWork = unitOfWork;
        _inventoryItemService = inventoryItemService;
        _customerInventoryService = customerInventoryService;
    }

    /// <summary>
    /// Xử lý khi thanh toán Stripe thành công (webhook).
    /// </summary>
    public async Task HandleSuccessfulPaymentAsync(string sessionId, string orderId)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("SessionId is required.");
        if (string.IsNullOrWhiteSpace(orderId))
            throw ErrorHelper.BadRequest("OrderId is required.");

        try
        {
            // Tìm transaction theo sessionId
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order).ThenInclude(o => o.OrderDetails)
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            var order = transaction.Payment?.Order;
            if (order == null)
                throw ErrorHelper.NotFound("Không tìm thấy order cho transaction này.");

            // Idempotency: Nếu đã PAID thì bỏ qua
            if (order.Status == OrderStatus.PAID.ToString())
            {
                _logger.Warn($"[HandleSuccessfulPaymentAsync] Order {orderId} đã ở trạng thái PAID, bỏ qua xử lý.");
                return;
            }

            _logger.Info($"[HandleSuccessfulPaymentAsync] OrderDetails count = {order.OrderDetails?.Count ?? 0}");

            // Cập nhật trạng thái transaction và payment
            transaction.Status = TransactionStatus.Successful.ToString();
            transaction.Payment.Status = PaymentStatus.Paid.ToString();
            transaction.Payment.PaidAt = DateTime.UtcNow;

            // Cập nhật trạng thái order
            order.Status = OrderStatus.PAID.ToString();
            order.CompletedAt = DateTime.UtcNow;

            // Lấy order details và tạo inventory item cho từng sản phẩm
            var orderDetails = await _unitOfWork.OrderDetails.GetAllAsync(od => od.OrderId == order.Id)
                ?? order.OrderDetails?.ToList() ?? new List<OrderDetail>();

            if (!orderDetails.Any())
            {
                _logger.Warn($"[HandleSuccessfulPaymentAsync] Không tìm thấy order details cho order {orderId}.");
                return;
            }

            var productCount = 0;
            var blindBoxCount = 0;
            foreach (var od in orderDetails)
            {
                if (od.ProductId.HasValue)
                {
                    _logger.Info($"[HandleSuccessfulPaymentAsync] Tạo inventory item cho sản phẩm {od.ProductId.Value} trong order {orderId}.");
                    var createDto = new CreateInventoryItemDto
                    {
                        ProductId = od.ProductId.Value,
                        Quantity = od.Quantity,
                        Location = string.Empty,
                        Status = "Active"
                    };
                    await _inventoryItemService.CreateAsync(createDto, order.UserId);
                    _logger.Success($"[HandleSuccessfulPaymentAsync] Đã tạo inventory item thứ {++productCount} cho sản phẩm {od.ProductId.Value} trong order {orderId}.");
                }

                if (od.BlindBoxId.HasValue)
                {
                    _logger.Info($"[HandleSuccessfulPaymentAsync] Tạo customer inventory cho BlindBox {od.BlindBoxId.Value} trong order {orderId}.");
                    for (var i = 0; i < od.Quantity; i++)
                    {
                        var createBlindBoxDto = new CreateCustomerInventoryDto
                        {
                            BlindBoxId = od.BlindBoxId.Value,
                            OrderDetailId = od.Id,
                            IsOpened = false
                        };
                        await _customerInventoryService.CreateAsync(createBlindBoxDto, order.UserId);
                        _logger.Success($"[HandleSuccessfulPaymentAsync] Đã tạo customer inventory thứ {++blindBoxCount} cho BlindBox {od.BlindBoxId.Value} trong order {orderId}.");
                    }
                }
            }

            await _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.Payments.Update(transaction.Payment);
            await _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.Success($"[HandleSuccessfulPaymentAsync] Đã xác nhận thanh toán thành công cho order {orderId}.");
            _logger.Success($"[HandleSuccessfulPaymentAsync] Đã cập nhật trạng thái PAID thành công cho {orderId}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandleSuccessfulPaymentAsync] {ex}");
            throw;
        }
    }

    /// <summary>
    /// Xử lý khi thanh toán Stripe thất bại hoặc session hết hạn.
    /// </summary>
    public async Task HandleFailedPaymentAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("SessionId is required.");

        try
        {
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order)
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            transaction.Status = TransactionStatus.Failed.ToString();
            if (transaction.Payment != null)
                transaction.Payment.Status = PaymentStatus.Failed.ToString();
            if (transaction.Payment?.Order != null)
                transaction.Payment.Order.Status = OrderStatus.FAILED.ToString();

            await _unitOfWork.Transactions.Update(transaction);
            if (transaction.Payment != null)
                await _unitOfWork.Payments.Update(transaction.Payment);
            if (transaction.Payment?.Order != null)
                await _unitOfWork.Orders.Update(transaction.Payment.Order);

            await _unitOfWork.SaveChangesAsync();
            _logger.Warn($"[HandleFailedPaymentAsync] Đã xử lý thất bại thanh toán cho session {sessionId}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandleFailedPaymentAsync] {ex}");
            throw;
        }
    }

    /// <summary>
    /// Xác nhận khi PaymentIntent được tạo (Stripe webhook).
    /// </summary>
    public async Task HandlePaymentIntentCreatedAsync(string paymentIntentId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId) || string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("PaymentIntentId và SessionId là bắt buộc.");

        try
        {
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            transaction.Payment.TransactionId = paymentIntentId;
            await _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.SaveChangesAsync();
            _logger.Info($"[HandlePaymentIntentCreatedAsync] Đã cập nhật PaymentIntentId cho transaction {transaction.Id}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandlePaymentIntentCreatedAsync] {ex}");
            throw;
        }
    }

    /// <summary>
    /// Lấy danh sách transaction của user hiện tại.
    /// </summary>
    public async Task<List<Transaction>> GetMyTransactionsAsync()
    {
        var userId = _claimsService.CurrentUserId;
        return await _unitOfWork.Transactions.GetQueryable()
            .Where(t => t.Payment.Order.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy danh sách transaction theo orderId.
    /// </summary>
    public async Task<List<Transaction>> GetTransactionsByOrderIdAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw ErrorHelper.BadRequest("OrderId is required.");

        return await _unitOfWork.Transactions.GetQueryable()
            .Include(t => t.Payment)
            .Where(t => t.Payment.OrderId == orderId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy chi tiết transaction theo Id.
    /// </summary>
    public async Task<Transaction?> GetTransactionByIdAsync(Guid transactionId)
    {
        if (transactionId == Guid.Empty)
            throw ErrorHelper.BadRequest("TransactionId is required.");

        return await _unitOfWork.Transactions.GetByIdAsync(transactionId);
    }
}