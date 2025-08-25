using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PayoutDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace BlindTreasure.Application.Services
{
    public class PayoutService : IPayoutService
    {
        private readonly IClaimsService _claimsService;
        private readonly ILoggerService _logger;
        private readonly IOrderService _orderService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrencyConversionService _currencyConversionService;
        private readonly IStripeService _stripeService;
        private readonly INotificationService _notificationService;
        private readonly IBlobService _blobService;


        public PayoutService(
            IClaimsService claimsService,
            ILoggerService logger,
            IOrderService orderService,
            IUnitOfWork unitOfWork,
            ICurrencyConversionService currencyConversionService,
            IStripeService stripeService,
            INotificationService notificationService,
            IBlobService blobService)
        {
            _claimsService = claimsService;
            _logger = logger;
            _orderService = orderService;
            _unitOfWork = unitOfWork;
            _currencyConversionService = currencyConversionService;
            _stripeService = stripeService;
            _notificationService = notificationService;
            _blobService = blobService;
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

            // Calculate current week period
            var currentMonday = now.Date.AddDays(-(int)now.DayOfWeek);
            var currentSunday = currentMonday.AddDays(6);

            // Find the latest payout for this seller
            var lastPayout = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.SellerId == sellerId)
                .OrderByDescending(p => p.PeriodEnd)
                .FirstOrDefaultAsync();

            DateTime periodStart;
            DateTime periodEnd;

            if (lastPayout != null && lastPayout.Status != PayoutStatus.PENDING && lastPayout.PeriodEnd >= currentMonday)
            {
                // Last payout for this week is already requested/completed, start new period from next Monday
                periodStart = lastPayout.PeriodEnd.AddDays(1); // Next Monday
                periodEnd = periodStart.AddDays(7);            // Next Monday
            }
            else
            {
                // No previous payout or still pending, use current week
                periodStart = currentMonday;
                periodEnd = periodStart.AddDays(7);
            }

            // Find existing pending payout for this period
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
                    OrderId = od.OrderId,
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

            order.PayoutId = payout.Id;
            await _unitOfWork.Payouts.Update(payout);
            await _unitOfWork.SaveChangesAsync();

            _logger.Success($"[Payout] Updated payout {payout.Id} for seller {sellerId}. Gross: {payout.GrossAmount:N0}, Net: {payout.NetAmount:N0}.");
        }

        /// <summary>
        /// Get eligible payout for seller (min 100,000 VND, và kiểm tra 7 ngày gần nhất nếu cần).
        /// </summary>
        public async Task<Payout?> GetEligiblePayoutForSellerAsync(Guid sellerId)
        {
            // Lấy payout gần nhất đã hoàn thành hoặc đang xử lý
            var lastPayout = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.SellerId == sellerId &&
                            (p.Status == PayoutStatus.COMPLETED || p.Status == PayoutStatus.PROCESSING))
                .OrderByDescending(p => p.CompletedAt ?? p.ProcessedAt ?? p.CreatedAt)
                .FirstOrDefaultAsync();

            var now = DateTime.UtcNow;
            if (lastPayout != null)
            {
                var lastDate = lastPayout.CompletedAt ?? lastPayout.ProcessedAt ?? lastPayout.CreatedAt;
                // Kiểm tra 7 ngày gần nhất (comment lại để test liên tục)
                // if ((now - lastDate).TotalDays < 7)
                // {
                //     _logger.Warn($"[Payout] Seller {sellerId} must wait 7 days between payouts. Last payout: {lastDate:yyyy-MM-dd}.");
                //     return null;
                // }
            }

            var pendingPayout = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.REQUESTED)
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
            var currentUserId = _claimsService.CurrentUserId;

            var payout = await GetEligiblePayoutForSellerAsync(sellerId);
            if (payout == null)
            {
                _logger.Warn($"[Payout] Seller {sellerId} is not eligible for payout.");
                return false;
            }

            // 1. Lấy thông tin seller và kiểm tra Stripe account
            var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId);
            if (seller == null || string.IsNullOrEmpty(seller.StripeAccountId))
            {
                _logger.Warn($"[Payout] Seller {sellerId} does not have a Stripe account.");
                return false;
            }

            // 2. Chuyển đổi tiền VND sang USD nếu cần
            var rate = await _currencyConversionService.GetVNDToUSDRate();
            if (rate == null || rate <= 0)
            {
                _logger.Warn("[Payout] Currency conversion rate is invalid.");
                return false;
            }
            // Stripe expects amount in smallest unit (cents)
            decimal usdAmount = payout.NetAmount / rate.Value * 100;

            // 3. Thực hiện chuyển tiền qua Stripe
            try
            {
                var transfer = await _stripeService.PayoutToSellerAsync(
                    seller.StripeAccountId,
                    usdAmount,
                    "usd",
                    $"Payout for seller {sellerId} - period {payout.PeriodStart:yyyy-MM-dd} to {payout.PeriodEnd:yyyy-MM-dd}");

                // 4. Cập nhật trạng thái payout và lưu thông tin giao dịch Stripe
                var oldStatus = payout.Status;
                payout.Status = PayoutStatus.PROCESSING;
                payout.ProcessedAt = DateTime.UtcNow;
                payout.StripeTransferId = transfer.Id;
                payout.StripeDestinationAccount = seller.StripeAccountId;
                await _unitOfWork.Payouts.Update(payout);

                var log = new PayoutLog
                {
                    PayoutId = payout.Id,
                    FromStatus = oldStatus,
                    ToStatus = PayoutStatus.PROCESSING,
                    Action = "SELLER_REQUEST",
                    Details = "Seller requested payout.",
                    TriggeredByUserId = currentUserId == Guid.Empty ? null : currentUserId,
                    LoggedAt = DateTime.UtcNow,
                    ErrorMessage = null
                };
                await _unitOfWork.PayoutLogs.AddAsync(log);

                await _unitOfWork.SaveChangesAsync();
                _logger.Success($"[Payout] Seller {sellerId} payout {payout.Id} moved to PROCESSING. Stripe transfer: {transfer.Id}");

                // Push notification to seller about payout
                var notificationDto = new NotificationDto
                {
                    Title = "Payout is being processed",
                    Message = $"Your payout for period {payout.PeriodStart:yyyy-MM-dd} to {payout.PeriodEnd:yyyy-MM-dd} is being processed. Amount: {payout.NetAmount:N0} VND.",
                    Type = NotificationType.System,
                    SourceUrl = null
                };
                await _notificationService.PushNotificationToUser(seller.UserId, notificationDto);

                return true;
            }
            catch (Exception ex)
            {
                // Ghi log lỗi vào payout log
                var log = new PayoutLog
                {
                    PayoutId = payout.Id,
                    FromStatus = PayoutStatus.REQUESTED,
                    ToStatus = PayoutStatus.FAILED,
                    Action = "SELLER_REQUEST",
                    Details = "Stripe payout failed.",
                    TriggeredByUserId = sellerId,
                    LoggedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
                await _unitOfWork.PayoutLogs.AddAsync(log);

                payout.Status = PayoutStatus.FAILED;
                await _unitOfWork.Payouts.Update(payout);
                await _unitOfWork.SaveChangesAsync();

                _logger.Warn($"[Payout] Stripe payout failed for seller {sellerId}: {ex.Message}");
                return false;
            }
        }

        public async Task<PayoutCalculationResultDto> GetUpcomingPayoutForCurrentSellerAsync()
        {
            try
            {
                var userId = _claimsService.CurrentUserId;
                var seller = await _unitOfWork.Sellers.GetQueryable()
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (seller == null)
                    throw new InvalidOperationException("Seller profile not found.");
                // Check if there is any payout in REQUESTED status
                var hasRequestedPayout = await _unitOfWork.Payouts.GetQueryable()
                    .FirstOrDefaultAsync(p => p.SellerId == seller.Id && p.Status == PayoutStatus.REQUESTED);

                var payout = await _unitOfWork.Payouts.GetQueryable().AsNoTracking()
                    .Include(p => p.PayoutDetails).ThenInclude(o => o.OrderDetail).ThenInclude(o => o.Order)
                    .FirstOrDefaultAsync(p => p.SellerId == seller.Id && p.Status == PayoutStatus.PENDING);

                if (payout == null)
                    return null;

                var orderDetailSummaries = payout.PayoutDetails.Select(pd => new PayoutDetailSummaryDto
                {
                    OrderDetailId = pd.OrderDetailId,
                    OrderId = pd.OrderDetail.OrderId,
                    Quantity = pd.OrderDetail.Quantity,
                    OriginalAmount = pd.OriginalAmount,
                    DiscountAmount = pd.DiscountAmount,
                    FinalAmount = pd.FinalAmount,
                    RefundAmount = pd.RefundAmount,
                    ContributedAmount = pd.ContributedAmount,
                    OrderCompletedAt = pd.OrderDetail.Order.CompletedAt ?? DateTime.MinValue
                }).ToList();


                // Map payout and details to PayoutCalculationResultDto (or a new DTO)
                foreach (var payoutDetail in payout.PayoutDetails)
                {
                    var payoutDetailSummaryDto = new PayoutDetailSummaryDto
                    {
                        OrderDetailId = payoutDetail.OrderDetailId,
                        OrderId = payoutDetail.OrderDetail.OrderId,
                        Quantity = payoutDetail.OrderDetail.Quantity,
                        OriginalAmount = payoutDetail.OriginalAmount,
                        DiscountAmount = payoutDetail.DiscountAmount,
                        FinalAmount = payoutDetail.FinalAmount,
                        RefundAmount = payoutDetail.RefundAmount,
                        ContributedAmount = payoutDetail.ContributedAmount,
                        OrderCompletedAt = payoutDetail.OrderDetail.Order.CompletedAt ?? DateTime.MinValue

                    };

                }

                // --- KIỂM TRA 7 NGÀY ---
                var lastPayout = await _unitOfWork.Payouts.GetQueryable()
                    .Where(p => p.SellerId == seller.Id &&
                                (p.Status == PayoutStatus.COMPLETED || p.Status == PayoutStatus.PROCESSING))
                    .OrderByDescending(p => p.CompletedAt ?? p.ProcessedAt ?? p.CreatedAt)
                    .FirstOrDefaultAsync();

                bool isWait7Days = false;
                string wait7DaysReason = null;
                if (lastPayout != null)
                {
                    var lastDate = lastPayout.CompletedAt ?? lastPayout.ProcessedAt ?? lastPayout.CreatedAt;
                    var daysSinceLast = (DateTime.UtcNow - lastDate).TotalDays;
                    if (daysSinceLast < 7)
                    {
                        isWait7Days = true;
                        wait7DaysReason = $"Bạn phải chờ đủ 7 ngày kể từ lần rút tiền gần nhất ({lastDate:yyyy-MM-dd}).";
                    }
                }

                var canPayout = seller.StripeAccountId != null
                                && payout.NetAmount >= 100_000m;
                //  && !isWait7Days;

                //var payoutBlockReason = canPayout ? null :
                //     isWait7Days ? wait7DaysReason :
                //    seller.StripeAccountId == null ? "Seller chưa liên kết Stripe account." :
                //    payout.NetAmount < 100_000m ? "Số tiền chưa đủ tối thiểu để rút." : null;


                var payoutBlockReason = canPayout ? null :
                    hasRequestedPayout != null ? $"Bạn đang có một yêu cầu rút tiền chưa được duyệt. PayoutId {hasRequestedPayout.Id}" :
                    seller.StripeAccountId == null ? "Seller chưa liên kết Stripe account." :
                    payout.NetAmount < 100_000m ? "Số tiền chưa đủ tối thiểu để rút." : null;

                return new PayoutCalculationResultDto
                {
                    SellerId = seller.Id,
                    SellerName = seller.CompanyName ?? seller.User?.FullName ?? "",
                    SellerEmail = seller.User?.Email ?? "",
                    StripeAccountId = seller.StripeAccountId,
                    GrossAmount = payout.GrossAmount,
                    PlatformFeeRate = payout.PlatformFeeRate,
                    PlatformFeeAmount = payout.PlatformFeeAmount,
                    NetAmount = payout.NetAmount,
                    TotalOrderDetails = orderDetailSummaries.Count,
                    TotalOrders = orderDetailSummaries.Select(x => x.OrderId).Distinct().Count(),
                    CanPayout = canPayout,
                    PayoutBlockReason = payoutBlockReason,
                    OrderDetailSummaries = orderDetailSummaries
                };
            }
            catch (Exception ex)
            {

                throw ErrorHelper.BadRequest(ex.Message);
            }
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
                RetryCount = p.RetryCount,
                ProofImageUrls = p.ProofImageUrls
            }).ToList();
        }

        public async Task<PayoutDetailResponseDto?> GetPayoutDetailByIdAsync(Guid payoutId)
        {
            var payout = await _unitOfWork.Payouts.GetQueryable()
                .Include(p => p.PayoutDetails).ThenInclude(o => o.OrderDetail).ThenInclude(o => o.Order)
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
                PayoutLogs = payoutLogs,
                ProofImageUrls = payout.ProofImageUrls
            };
        }

        /// <summary>
        /// Seller gửi yêu cầu rút tiền cho payout đang PENDING.
        /// Chuyển trạng thái từ PENDING sang REQUESTED, ghi log.
        /// </summary>
        public async Task<PayoutDetailResponseDto?> RequestPayoutAsync(Guid sellerId)
        {
            var userId = _claimsService.CurrentUserId;

            // Check if there is any payout in REQUESTED status for this seller
            var hasRequestedPayout = await _unitOfWork.Payouts.GetQueryable()
                .FirstOrDefaultAsync(p => p.SellerId == sellerId && p.Status == PayoutStatus.REQUESTED);

            if (hasRequestedPayout != null)
            {
                throw ErrorHelper.BadRequest($"[Payout] Seller {sellerId} đã có một yêu cầu rút tiền chưa được duyệt. PayoutId {hasRequestedPayout.Id}");
            }

            // Lấy thông tin seller để kiểm tra StripeAccountId
            var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId);
            if (seller == null)
            {
                _logger.Warn($"[Payout] Seller {sellerId} không tồn tại.");
                return null;
            }

            // Tìm payout đang PENDING của seller
            var payout = await _unitOfWork.Payouts.GetQueryable()
                .Include(p => p.PayoutDetails).ThenInclude(pd => pd.OrderDetail).ThenInclude(od => od.Order)
                .Include(p => p.PayoutLogs)
                .Include(p => p.Seller).ThenInclude(s => s.User)
                .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.PENDING)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (payout == null)
            {
                _logger.Warn($"[Payout] Seller {sellerId} không có payout nào ở trạng thái PENDING.");
                return null;
            }

            // Kiểm tra điều kiện canPayout
            bool canPayout = seller.StripeAccountId != null
                     && payout.NetAmount >= 100_000m;

            if (!canPayout)
            {
                var reason = seller.StripeAccountId == null
                    ? "Seller chưa liên kết Stripe account."
                    : payout.NetAmount < 100_000m
                        ? "Số tiền chưa đủ tối thiểu để rút."
                        : "Không đủ điều kiện rút tiền.";
                _logger.Warn($"[Payout] Seller {sellerId} không đủ điều kiện rút tiền: {reason}");
                throw ErrorHelper.BadRequest($"[Payout] {reason}");
            }

            // Update PeriodEnd to Sunday of current week
            var now = DateTime.UtcNow;
            payout.PeriodEnd = now.Date.AddDays(7 - (int)now.DayOfWeek - 1);

            // Chuyển trạng thái sang REQUESTED
            payout.Status = PayoutStatus.REQUESTED;
            payout.ProcessedAt = now;
            await _unitOfWork.Payouts.Update(payout);

            // Ghi log yêu cầu rút tiền
            var log = new PayoutLog
            {
                PayoutId = payout.Id,
                FromStatus = PayoutStatus.PENDING,
                ToStatus = PayoutStatus.REQUESTED,
                Action = "SELLER_REQUEST",
                Details = "Seller gửi yêu cầu rút tiền.",
                TriggeredByUserId = userId != Guid.Empty ? userId : null,
                LoggedAt = DateTime.UtcNow
            };
            await _unitOfWork.PayoutLogs.AddAsync(log);

            await _unitOfWork.SaveChangesAsync();
            _logger.Success($"[Payout] Seller {sellerId} đã gửi yêu cầu rút tiền cho payout {payout.Id}.");

            // Trả về chi tiết payout sau khi cập nhật
            return await GetPayoutDetailByIdAsync(payout.Id);
        }


        private MemoryStream GeneratePayoutExcel(List<Payout> payouts, Seller seller)
        {
            ExcelPackage.License.SetNonCommercialPersonal("your-name-or-organization");
            var package = new ExcelPackage();

            // Sheet 1: Payouts
            var ws = package.Workbook.Worksheets.Add("Payouts");
            string[] headers = {
        "SellerName", "SellerEmail", "StripeAccountId", "PeriodStart", "PeriodEnd", "GrossAmount",
        "PlatformFeeAmount", "NetAmount", "Status", "CreatedAt", "ProcessedAt", "CompletedAt",
        "StripeTransferId", "FailureReason"
    };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                ws.Cells[1, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            for (int i = 0; i < payouts.Count; i++)
            {
                var p = payouts[i];
                ws.Cells[i + 2, 1].Value = seller.CompanyName ?? seller.User?.FullName ?? "";
                ws.Cells[i + 2, 2].Value = seller.User?.Email ?? "";
                ws.Cells[i + 2, 3].Value = seller.StripeAccountId ?? "";
                ws.Cells[i + 2, 4].Value = p.PeriodStart.ToString("yyyy-MM-dd");
                ws.Cells[i + 2, 5].Value = p.PeriodEnd.ToString("yyyy-MM-dd");
                ws.Cells[i + 2, 6].Value = p.GrossAmount;
                ws.Cells[i + 2, 7].Value = p.PlatformFeeAmount;
                ws.Cells[i + 2, 8].Value = p.NetAmount;
                ws.Cells[i + 2, 9].Value = p.Status.ToString();
                ws.Cells[i + 2, 10].Value = p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                ws.Cells[i + 2, 11].Value = p.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                ws.Cells[i + 2, 12].Value = p.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                ws.Cells[i + 2, 13].Value = p.StripeTransferId ?? "";
                ws.Cells[i + 2, 14].Value = p.FailureReason ?? "";
            }
            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            // Sheet 2: Payout Details
            var wsDetail = package.Workbook.Worksheets.Add("Payout Details");
            string[] detailHeaders = {
        "PayoutId", "OrderDetailId", "OrderId", "ProductName", "Quantity", "OriginalAmount",
        "DiscountAmount", "FinalAmount", "RefundAmount", "ContributedAmount", "OrderCompletedAt"
    };
            for (int i = 0; i < detailHeaders.Length; i++)
            {
                wsDetail.Cells[1, i + 1].Value = detailHeaders[i];
                wsDetail.Cells[1, i + 1].Style.Font.Bold = true;
                wsDetail.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                wsDetail.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                wsDetail.Cells[1, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            int row = 2;
            foreach (var p in payouts)
            {
                foreach (var pd in p.PayoutDetails)
                {
                    wsDetail.Cells[row, 1].Value = p.Id.ToString();
                    wsDetail.Cells[row, 2].Value = pd.OrderDetailId.ToString();
                    wsDetail.Cells[row, 3].Value = pd.OrderDetail.OrderId.ToString();
                    wsDetail.Cells[row, 4].Value = pd.OrderDetail.Product?.Name ?? "";
                    wsDetail.Cells[row, 5].Value = pd.OrderDetail.Quantity;
                    wsDetail.Cells[row, 6].Value = pd.OriginalAmount;
                    wsDetail.Cells[row, 7].Value = pd.DiscountAmount;
                    wsDetail.Cells[row, 8].Value = pd.FinalAmount;
                    wsDetail.Cells[row, 9].Value = pd.RefundAmount;
                    wsDetail.Cells[row, 10].Value = pd.ContributedAmount;
                    wsDetail.Cells[row, 11].Value = pd.OrderDetail.Order?.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                    row++;
                }
            }
            wsDetail.Cells[wsDetail.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);
            if (stream.CanSeek) stream.Position = 0;
            return stream;
        }

        public async Task<MemoryStream> ExportLatestPayoutProofAsync()
        {
            var userId = _claimsService.CurrentUserId;
            var seller = await _unitOfWork.Sellers.GetQueryable()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (seller == null)
                throw ErrorHelper.BadRequest("Seller profile not existing.");

            var payout = await _unitOfWork.Payouts.GetQueryable()
                .Include(p => p.PayoutDetails).ThenInclude(pd => pd.OrderDetail)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(p => p.SellerId == seller.Id && p.Status == PayoutStatus.PROCESSING);
            if (payout == null)
                throw ErrorHelper.BadRequest("Not found the newest handling payout to show");

            return GeneratePayoutExcel(new List<Payout> { payout }, seller);
        }

        public async Task<MemoryStream> ExportPayoutByIdAsync(Guid payoutId)
        {
            var userId = _claimsService.CurrentUserId;
            var seller = await _unitOfWork.Sellers.GetQueryable()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (seller == null)
                throw new InvalidOperationException("Seller profile not found.");

            var payout = await _unitOfWork.Payouts.GetQueryable()
                .Include(p => p.PayoutDetails).ThenInclude(pd => pd.OrderDetail)
                .FirstOrDefaultAsync(p => p.Id == payoutId && p.SellerId == seller.Id);

            if (payout == null)
                throw new InvalidOperationException("Payout not found.");

            return GeneratePayoutExcel(new List<Payout> { payout }, seller);
        }

        public async Task<Pagination<PayoutListResponseDto>> GetPayoutsForAdminAsync(PayoutAdminQueryParameter param)
        {
            var query = BuildPayoutQuery(param);

            var totalCount = await query.CountAsync();

            List<Payout> payouts;
            if (param.PageIndex == 0)
                payouts = await query.ToListAsync();
            else
                payouts = await query
                    .Skip((param.PageIndex - 1) * param.PageSize)
                    .Take(param.PageSize)
                    .ToListAsync();

            var items = payouts.Select(p => new PayoutListResponseDto
            {
                Id = p.Id,
                SellerId = p.SellerId,
                SellerName = p.Seller.CompanyName ?? p.Seller.User?.FullName ?? "",
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
                RetryCount = p.RetryCount,
                ProofImageUrls = p.ProofImageUrls
            }).ToList();

            return new Pagination<PayoutListResponseDto>(items, totalCount, param.PageIndex, param.PageSize);
        }

        private IQueryable<Payout> BuildPayoutQuery(PayoutAdminQueryParameter param, Guid? sellerId = null)
        {
            var query = _unitOfWork.Payouts.GetQueryable()
                .Include(p => p.Seller).ThenInclude(s => s.User)
                .Include(p => p.PayoutDetails).ThenInclude(p => p.OrderDetail)
                .Where(p => !p.Seller.IsDeleted);

            if (sellerId.HasValue)
                query = query.Where(p => p.SellerId == sellerId.Value);

            if (param.Status.HasValue)
                query = query.Where(p => p.Status == param.Status.Value);

            if (param.SellerId.HasValue)
                query = query.Where(p => p.SellerId == param.SellerId.Value);

            if (param.PeriodStart.HasValue)
                query = query.Where(p => p.PeriodStart >= param.PeriodStart.Value);

            if (param.PeriodEnd.HasValue)
                query = query.Where(p => p.PeriodEnd <= param.PeriodEnd.Value);

            return query.OrderByDescending(p => p.CreatedAt);
        }

        public async Task<Pagination<PayoutListResponseDto>> GetPayoutsForCurrentSellerAsync(PayoutAdminQueryParameter param)
        {
            var userId = _claimsService.CurrentUserId;
            var seller = await _unitOfWork.Sellers.GetQueryable()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (seller == null)
                throw ErrorHelper.BadRequest("Seller profile not found.");

            var query = BuildPayoutQuery(param, seller.Id);

            var totalCount = await query.CountAsync();

            List<Payout> payouts;
            if (param.PageIndex == 0)
                payouts = await query.ToListAsync();
            else
                payouts = await query
                    .Skip((param.PageIndex - 1) * param.PageSize)
                    .Take(param.PageSize)
                    .ToListAsync();

            var items = payouts.Select(p => new PayoutListResponseDto
            {
                Id = p.Id,
                SellerId = p.SellerId,
                SellerName = p.Seller.CompanyName ?? p.Seller.User?.FullName ?? "",
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
                RetryCount = p.RetryCount,
                ProofImageUrls = p.ProofImageUrls
            }).ToList();

            return new Pagination<PayoutListResponseDto>(items, totalCount, param.PageIndex, param.PageSize);
        }

        private async Task<List<string>> UploadProofImagesAsync(Guid payoutId, List<IFormFile> files)
        {
            var uploadedUrls = new List<string>();
            foreach (var file in files.Where(f => f.Length > 0).Take(6))
            {
                var fileExtension = Path.GetExtension(file.FileName);
                var fileName = $"payouts/proof_{payoutId}_{Guid.NewGuid():N}{fileExtension}";
                await using var stream = file.OpenReadStream();
                await _blobService.UploadFileAsync(fileName, stream);
                var fileUrl = await _blobService.GetPreviewUrlAsync(fileName);
                if (string.IsNullOrEmpty(fileUrl))
                    throw ErrorHelper.Internal("Cannot get proof image url.");
                uploadedUrls.Add(fileUrl);
            }
            return uploadedUrls;
        }

        public async Task<PayoutDetailResponseDto?> AdminConfirmPayoutWithProofAsync(Guid payoutId, List<IFormFile> files, Guid adminUserId)
        {
            if (files == null || files.Count == 0 || files.All(f => f.Length == 0))
                throw ErrorHelper.BadRequest("No valid proof images provided.");

            var payout = await _unitOfWork.Payouts.GetQueryable()
                .Include(p => p.Seller).ThenInclude(s => s.User)
                .Include(p => p.PayoutLogs)
                .FirstOrDefaultAsync(p => p.Id == payoutId);

            if (payout == null)
                throw ErrorHelper.NotFound("Payout not found.");

            if (payout.Status != PayoutStatus.PROCESSING)
                throw ErrorHelper.BadRequest("Only payouts in PROCESSING status can be confirmed.");

            // Use helper to upload images
            var uploadedUrls = await UploadProofImagesAsync(payoutId, files);

            // Update payout
            payout.ProofImageUrls = uploadedUrls;
            payout.Status = PayoutStatus.COMPLETED;
            payout.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.Payouts.Update(payout);

            // Log the status change
            var log = new PayoutLog
            {
                PayoutId = payout.Id,
                FromStatus = PayoutStatus.PROCESSING,
                ToStatus = PayoutStatus.COMPLETED,
                Action = "ADMIN_CONFIRM",
                Details = $"Admin confirmed payout and uploaded {uploadedUrls.Count} proof images.",
                TriggeredByUserId = adminUserId,
                LoggedAt = DateTime.UtcNow
            };
            await _unitOfWork.PayoutLogs.AddAsync(log);

            await _unitOfWork.SaveChangesAsync();

            _logger.Success($"[Payout] Admin confirmed payout {payout.Id} for seller {payout.SellerId}.");

            // Notify seller
            if (payout.Seller?.User != null)
            {
                var notificationDto = new NotificationDto
                {
                    Title = "Payout Completed",
                    Message = $"Your payout for period {payout.PeriodStart:yyyy-MM-dd} to {payout.PeriodEnd:yyyy-MM-dd} has been completed. Please check your account.",
                    Type = NotificationType.System,
                    SourceUrl = null
                };
                await _notificationService.PushNotificationToUser(payout.Seller.User.Id, notificationDto);
            }

            // Return updated payout details
            return await GetPayoutDetailByIdAsync(payout.Id);
        }
    }
}
