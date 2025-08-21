using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services
{
    public class PayoutService : IPayoutService
    {
        private readonly IClaimsService _claimsService;
        private readonly ILoggerService _logger;
        private readonly IOrderService _orderService;
        private readonly IUnitOfWork _unitOfWork;

        public PayoutService(
            IClaimsService claimsService,
            ILoggerService logger,
            IOrderService orderService,
            IUnitOfWork unitOfWork)
        {
            _claimsService = claimsService;
            _logger = logger;
            _orderService = orderService;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Add completed order to seller's pending payout for the current period.
        /// </summary>
        public async Task AddCompletedOrderToPayoutAsync(Order order, CancellationToken? ct = default)
        {
            if (order == null || order.Status != OrderStatus.COMPLETED.ToString())
            {
                _logger.Warn($"[Payout] Order {order?.Id} is not completed. Skipping payout accumulation.");
                return;
            }

            var sellerId = order.SellerId;
            var now = DateTime.UtcNow;

            // Determine current payout period (weekly, starting Monday)
            var periodStart = now.Date.AddDays(-(int)now.DayOfWeek);
            var periodEnd = periodStart.AddDays(7);

            // Find existing pending payout
            var payout = await _unitOfWork.Payouts.GetQueryable()
                .FirstOrDefaultAsync(p =>
                    p.SellerId == sellerId &&
                    p.PeriodStart == periodStart &&
                    p.PeriodEnd == periodEnd &&
                    p.Status == PayoutStatus.PENDING, ct.Value);

            if (payout == null)
            {
                payout = new Payout
                {
                    SellerId = sellerId,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    PeriodType = PayoutPeriodType.WEEKLY,
                    Status = PayoutStatus.PENDING,
                    GrossAmount = 0,
                    PlatformFeeRate = 5.0m, // Example: 5%
                    PlatformFeeAmount = 0,
                    NetAmount = 0
                };
                payout = await _unitOfWork.Payouts.AddAsync(payout);
                _logger.Info($"[Payout] Created new pending payout for seller {sellerId} for period {periodStart:yyyy-MM-dd} - {periodEnd:yyyy-MM-dd}.");
            }

            foreach (var od in order.OrderDetails.Where(od => od.Status != OrderDetailItemStatus.CANCELLED))
            {
                // Prevent duplicate payout details for the same order detail
                var exists = await _unitOfWork.PayoutDetails.GetQueryable()
                    .AnyAsync(pd => pd.PayoutId == payout.Id && pd.OrderDetailId == od.Id, ct.Value);
                if (exists)
                {
                    _logger.Warn($"[Payout] OrderDetail {od.Id} already included in payout {payout.Id}. Skipping.");
                    continue;
                }

                var payoutDetail = new PayoutDetail
                {
                    PayoutId = payout.Id,
                    OrderDetailId = od.Id,
                    OriginalAmount = od.TotalPrice,
                    DiscountAmount = od.DetailDiscountPromotion ?? 0,
                    FinalAmount = od.FinalDetailPrice ?? od.TotalPrice,
                    RefundAmount = 0, // Update if refunds exist
                    ContributedAmount = od.FinalDetailPrice ?? od.TotalPrice,
                    CalculatedAt = now
                };
                await _unitOfWork.PayoutDetails.AddAsync(payoutDetail);
                payout.GrossAmount += payoutDetail.ContributedAmount;
                _logger.Info($"[Payout] Added OrderDetail {od.Id} to payout {payout.Id}.");
            }

            payout.PlatformFeeAmount = Math.Round(payout.GrossAmount * payout.PlatformFeeRate / 100m, 2);
            payout.NetAmount = payout.GrossAmount - payout.PlatformFeeAmount;

            await _unitOfWork.Payouts.Update(payout);
            await _unitOfWork.SaveChangesAsync();

            _logger.Success($"[Payout] Updated payout {payout.Id} for seller {sellerId}. Gross: {payout.GrossAmount:N0}, Net: {payout.NetAmount:N0}.");
        }

        /// <summary>
        /// Get eligible payout for seller (min 10 days since last payout, min 100,000 VND).
        /// </summary>
        public async Task<Payout?> GetEligiblePayoutForSellerAsync(Guid sellerId)
        {
            var lastPayout = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.SellerId == sellerId &&
                            (p.Status == PayoutStatus.COMPLETED || p.Status == PayoutStatus.PROCESSING))
                .OrderByDescending(p => p.CompletedAt ?? p.ProcessedAt)
                .FirstOrDefaultAsync();

            var now = DateTime.UtcNow;
            if (lastPayout != null)
            {
                var lastDate = lastPayout.CompletedAt ?? lastPayout.ProcessedAt ?? lastPayout.CreatedAt;
                if ((now - lastDate).TotalDays < 10)
                {
                    _logger.Warn($"[Payout] Seller {sellerId} must wait 10 days between payouts. Last payout: {lastDate:yyyy-MM-dd}.");
                    return null;
                }
            }

            var pendingPayout = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.PENDING)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingPayout == null || pendingPayout.NetAmount < 100_000m)
            {
                _logger.Warn($"[Payout] Seller {sellerId} does not have enough funds for payout. NetAmount: {pendingPayout?.NetAmount:N0}.");
                return null;
            }

            _logger.Info($"[Payout] Seller {sellerId} is eligible for payout. NetAmount: {pendingPayout.NetAmount:N0}.");
            return pendingPayout;
        }

        /// <summary>
        /// Process payout request for seller (update status, log, trigger payment).
        /// </summary>
        public async Task<bool> ProcessSellerPayoutAsync(Guid sellerId)
        {
            var payout = await GetEligiblePayoutForSellerAsync(sellerId);
            if (payout == null)
            {
                _logger.Warn($"[Payout] Seller {sellerId} is not eligible for payout.");
                return false;
            }

            payout.Status = PayoutStatus.PROCESSING;
            payout.ProcessedAt = DateTime.UtcNow;
            await _unitOfWork.Payouts.Update(payout);

            var log = new PayoutLog
            {
                PayoutId = payout.Id,
                FromStatus = PayoutStatus.PENDING,
                ToStatus = PayoutStatus.PROCESSING,
                Action = "SELLER_REQUEST",
                Details = "Seller requested payout.",
                TriggeredByUserId = sellerId,
                LoggedAt = DateTime.UtcNow
            };
            await _unitOfWork.PayoutLogs.AddAsync(log);

            await _unitOfWork.SaveChangesAsync();
            _logger.Success($"[Payout] Seller {sellerId} payout {payout.Id} moved to PROCESSING.");

            // TODO: Trigger Stripe payout here if needed

            return true;
        }
    }
}
