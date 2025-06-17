using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapper;
    private readonly IOrderService _orderService;
    private readonly IUnitOfWork _unitOfWork;

    public TransactionService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IMapperService mapper,
        IOrderService orderService,
        IUnitOfWork unitOfWork)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _mapper = mapper;
        _orderService = orderService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Xử lý khi thanh toán Stripe thành công (webhook).
    /// </summary>
    public async Task HandleSuccessfulPaymentAsync(string sessionId, string orderId)
    {
        try
        {
            // Tìm transaction theo sessionId
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order)
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            // Cập nhật trạng thái transaction và payment
            transaction.Status = "Success";
            transaction.Payment.Status = "Paid";
            transaction.Payment.PaidAt = DateTime.UtcNow;

            // Cập nhật trạng thái order
            if (transaction.Payment.Order != null)
            {
                transaction.Payment.Order.Status = OrderStatus.PAID.ToString();
                transaction.Payment.Order.CompletedAt = DateTime.UtcNow;
            }

            await _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.Payments.Update(transaction.Payment);
            if (transaction.Payment.Order != null)
                await _unitOfWork.Orders.Update(transaction.Payment.Order);

            await _unitOfWork.SaveChangesAsync();
            _loggerService.Success($"[HandleSuccessfulPaymentAsync] Đã xác nhận thanh toán thành công cho order {orderId}.");
            _loggerService.Success($"[HandleSuccessfulPaymentAsync] Đã cập nhật trang thái PAID thành công cho {orderId}.");
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[HandleSuccessfulPaymentAsync] {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Xử lý khi thanh toán Stripe thất bại hoặc session hết hạn.
    /// </summary>
    public async Task HandleFailedPaymentAsync(string sessionId)
    {
        try
        {
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order)
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            transaction.Status = "Failed";
            if (transaction.Payment != null)
                transaction.Payment.Status = "Failed";
            if (transaction.Payment?.Order != null)
                transaction.Payment.Order.Status = OrderStatus.FAILED.ToString();

            await _unitOfWork.Transactions.Update(transaction);
            if (transaction.Payment != null)
                await _unitOfWork.Payments.Update(transaction.Payment);
            if (transaction.Payment?.Order != null)
                await _unitOfWork.Orders.Update(transaction.Payment.Order);

            await _unitOfWork.SaveChangesAsync();
            _loggerService.Warn($"[HandleFailedPaymentAsync] Đã xử lý thất bại thanh toán cho session {sessionId}.");
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[HandleFailedPaymentAsync] {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Xác nhận khi PaymentIntent được tạo (Stripe webhook).
    /// </summary>
    public async Task HandlePaymentIntentCreatedAsync(string paymentIntentId, string sessionId)
    {
        try
        {
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            transaction.Payment.TransactionId = paymentIntentId;
            await _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.Info($"[HandlePaymentIntentCreatedAsync] Đã cập nhật PaymentIntentId cho transaction {transaction.Id}.");
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[HandlePaymentIntentCreatedAsync] {ex.Message}");
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
        return await _unitOfWork.Transactions.GetByIdAsync(transactionId);
    }
}