using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.PayoutDTOs;
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
                await _unitOfWork.SaveChangesAsync(); // <-- Add this line
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

        public async Task<PayoutCalculationResultDto> CalculateUpcomingPayoutForCurrentSellerAsync(PayoutCalculationRequestDto req)
        {
            var userId = _claimsService.CurrentUserId;
            var seller = await _unitOfWork.Sellers.GetQueryable()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (seller == null)
                throw new InvalidOperationException("Seller profile not found.");

            var periodStart = req.PeriodStart.Date;
            var periodEnd = req.PeriodEnd.Date;

            // Get all completed orders for seller in period, not yet included in a payout
            var completedOrderDetails = await _unitOfWork.OrderDetails.GetQueryable()
                .Include(od => od.Order)
                .Include(od => od.Product)
                .Where(od =>
                    od.Order.SellerId == seller.Id &&
                    od.Order.Status == OrderStatus.COMPLETED.ToString() &&
                    od.Order.CompletedAt >= periodStart &&
                    od.Order.CompletedAt < periodEnd &&
                    !od.IsDeleted &&
                    !_unitOfWork.PayoutDetails.GetQueryable().Any(pd => pd.OrderDetailId == od.Id)
                )
                .ToListAsync();

            var grossAmount = completedOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice);
            var platformFeeRate = req.CustomPlatformFeeRate ?? 5.0m;
            var platformFeeAmount = Math.Round(grossAmount * platformFeeRate / 100m, 2);
            var netAmount = grossAmount - platformFeeAmount;

            var orderDetailSummaries = completedOrderDetails.Select(od => new PayoutDetailSummaryDto
            {
                OrderDetailId = od.Id,
                OrderId = od.OrderId,
                ProductName = od.Product?.Name ?? "",
                Quantity = od.Quantity,
                OriginalAmount = od.TotalPrice,
                DiscountAmount = od.DetailDiscountPromotion ?? 0,
                FinalAmount = od.FinalDetailPrice ?? od.TotalPrice,
                RefundAmount = 0, // Update if refunds exist
                ContributedAmount = od.FinalDetailPrice ?? od.TotalPrice,
                OrderCompletedAt = od.Order.CompletedAt ?? DateTime.MinValue
            }).ToList();

            var canPayout = seller.StripeAccountId != null && netAmount >= 100_000m;
            var payoutBlockReason = canPayout ? null :
                seller.StripeAccountId == null ? "Seller chưa liên kết Stripe account." :
                netAmount < 100_000m ? "Số tiền chưa đủ tối thiểu để rút." : null;

            return new PayoutCalculationResultDto
            {
                SellerId = seller.Id,
                SellerName = seller.CompanyName ?? seller.User?.FullName ?? "",
                SellerEmail = seller.User?.Email ?? "",
                StripeAccountId = seller.StripeAccountId,
                GrossAmount = grossAmount,
                PlatformFeeRate = platformFeeRate,
                PlatformFeeAmount = platformFeeAmount,
                NetAmount = netAmount,
                TotalOrderDetails = orderDetailSummaries.Count,
                TotalOrders = orderDetailSummaries.Select(x => x.OrderId).Distinct().Count(),
                CanPayout = canPayout,
                PayoutBlockReason = payoutBlockReason,
                OrderDetailSummaries = orderDetailSummaries
            };
        }

        public async Task<List<PayoutListResponseDto>> GetSellerPayoutsForPeriodAsync(PayoutCalculationRequestDto req)
        {
            var userId = _claimsService.CurrentUserId;
            var seller = await _unitOfWork.Sellers.GetQueryable()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (seller == null)
                throw new InvalidOperationException("Seller profile not found.");

            var periodStart = req.PeriodStart.Date;
            var periodEnd = req.PeriodEnd.Date;

            var payouts = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.SellerId == seller.Id &&
                            p.PeriodStart >= periodStart &&
                            p.PeriodEnd <= periodEnd)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return payouts.Select(p => new PayoutListResponseDto
            {
                Id = p.Id,
                SellerId = p.SellerId,
                SellerName = seller.CompanyName ?? seller.User?.FullName ?? "",
                PeriodStart = p.PeriodStart,
                PeriodEnd = p.PeriodEnd,
                PeriodType = p.PeriodType.ToString(),
                GrossAmount = p.GrossAmount,
                NetAmount = p.NetAmount,
                PlatformFeeAmount = p.PlatformFeeAmount,
                Status = p.Status.ToString(),
                CreatedAt = p.CreatedAt,
                ProcessedAt = p.ProcessedAt,
                CompletedAt = p.CompletedAt,
                StripeTransferId = p.StripeTransferId,
                FailureReason = p.FailureReason,
                RetryCount = p.RetryCount
            }).ToList();
        }

        public async Task<PayoutDetailResponseDto?> GetPayoutDetailByIdAsync(Guid payoutId)
        {
            var payout = await _unitOfWork.Payouts.GetQueryable()
                .Include(p => p.PayoutDetails)
                .Include(p => p.PayoutLogs)
                .Include(p => p.Seller).ThenInclude(s => s.User)
                .FirstOrDefaultAsync(p => p.Id == payoutId);

            if (payout == null)
                return null;

            var seller = payout.Seller;
            var payoutDetails = payout.PayoutDetails.Select(pd => new PayoutDetailSummaryDto
            {
                OrderDetailId = pd.OrderDetailId,
                OrderId = pd.OrderDetail.OrderId,
                ProductName = pd.OrderDetail.Product?.Name ?? "",
                Quantity = pd.OrderDetail.Quantity,
                OriginalAmount = pd.OriginalAmount,
                DiscountAmount = pd.DiscountAmount,
                FinalAmount = pd.FinalAmount,
                RefundAmount = pd.RefundAmount,
                ContributedAmount = pd.ContributedAmount,
                OrderCompletedAt = pd.OrderDetail.Order.CompletedAt ?? DateTime.MinValue
            }).ToList();

            var payoutLogs = payout.PayoutLogs.Select(log => new PayoutLogDto
            {
                Id = log.Id,
                FromStatus = log.FromStatus.ToString(),
                ToStatus = log.ToStatus.ToString(),
                Action = log.Action,
                Details = log.Details,
                ErrorMessage = log.ErrorMessage,
                TriggeredByUserName = log.TriggeredByUser?.FullName ?? "",
                LoggedAt = log.LoggedAt
            }).ToList();

            return new PayoutDetailResponseDto
            {
                Id = payout.Id,
                SellerId = seller.Id,
                SellerName = seller.CompanyName ?? seller.User?.FullName ?? "",
                SellerEmail = seller.User?.Email ?? "",
                PeriodStart = payout.PeriodStart,
                PeriodEnd = payout.PeriodEnd,
                PeriodType = payout.PeriodType.ToString(),
                GrossAmount = payout.GrossAmount,
                PlatformFeeRate = payout.PlatformFeeRate,
                PlatformFeeAmount = payout.PlatformFeeAmount,
                NetAmount = payout.NetAmount,
                Status = payout.Status.ToString(),
                CreatedAt = payout.CreatedAt,
                ProcessedAt = payout.ProcessedAt,
                CompletedAt = payout.CompletedAt,
                StripeTransferId = payout.StripeTransferId,
                StripeDestinationAccount = payout.StripeDestinationAccount,
                Notes = payout.Notes,
                FailureReason = payout.FailureReason,
                RetryCount = payout.RetryCount,
                NextRetryAt = payout.NextRetryAt,
                PayoutDetails = payoutDetails,
                PayoutLogs = payoutLogs
            };
        }
    }
}
