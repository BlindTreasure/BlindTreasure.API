using System.Drawing;
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
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace BlindTreasure.Application.Services;

public class PayoutService : IPayoutService
{
    private const decimal MINIMUM_PAYOUT_AMOUNT = 100_000m;
    private const decimal PLATFORM_FEE_RATE = 5.0m;
    private const int MINIMUM_DAYS_BETWEEN_PAYOUTS = 7;
    private const int MAX_PROOF_IMAGES = 6;
    private readonly IBlobService _blobService;
    private readonly IClaimsService _claimsService;
    private readonly ICurrencyConversionService _currencyConversionService;
    private readonly ILoggerService _logger;
    private readonly INotificationService _notificationService;
    private readonly IStripeService _stripeService;
    private readonly IUnitOfWork _unitOfWork;

    public PayoutService(
        IClaimsService claimsService,
        ILoggerService logger,
        IUnitOfWork unitOfWork,
        ICurrencyConversionService currencyConversionService,
        IStripeService stripeService,
        INotificationService notificationService,
        IBlobService blobService)
    {
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _currencyConversionService = currencyConversionService;
        _stripeService = stripeService;
        _notificationService = notificationService;
        _blobService = blobService;
    }

    #region Public Methods

    public async Task AddCompletedOrderToPayoutAsync(Order order, CancellationToken? ct = default)
    {
        try
        {
            if (!IsOrderEligibleForPayout(order))
            {
                _logger.Warn($"[Payout] Order {order?.Id} is not eligible for payout accumulation.");
                return;
            }

            var payout = await GetOrCreatePendingPayoutAsync(order.SellerId, ct, order);
            await AddOrderDetailsToPayoutAsync(payout, order, ct);
            await UpdatePayoutAmountsAsync(payout, order);

            //var result = await _unitOfWork.SaveChangesAsync();

            _logger.Success(
                $"[Payout] Updated payout {payout.Id} for seller {order.SellerId}. Gross: {payout.GrossAmount:N0}, Net: {payout.NetAmount:N0}.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
            throw;
        }
    }

    public async Task<Payout?> GetEligiblePayoutForSellerAsync(Guid sellerId)
    {
        // Check 7-day waiting period (commented for testing)
        //if (!await IsSellerEligibleForPayoutAsync(sellerId))
        //    return null;

        var pendingPayout = await GetMainActivePayoutWithDetailsAsync(sellerId);
        if (pendingPayout == null || pendingPayout.NetAmount < MINIMUM_PAYOUT_AMOUNT)
        {
            _logger.Warn(
                $"[Payout] Seller {sellerId} does not have enough funds for payout. NetAmount: {pendingPayout?.NetAmount:N0}.");
            return null;
        }

        _logger.Info($"[Payout] Seller {sellerId} is eligible for payout. NetAmount: {pendingPayout.NetAmount:N0}.");
        return pendingPayout;
    }

    public async Task<PayoutListResponseDto?> GetEligiblePayoutDtoForSellerAsync(Guid sellerId)
    {
        // Check 7-day waiting period (commented for testing)
        //if (!await IsSellerEligibleForPayoutAsync(sellerId))
        //    return null;

        var payout = await GetMainActivePayoutWithDetailsAsync(sellerId);
        if (payout == null || payout.NetAmount < MINIMUM_PAYOUT_AMOUNT)
        {
            _logger.Warn(
                $"[Payout] Seller {sellerId} does not have enough funds for payout. NetAmount: {payout?.NetAmount:N0}.");
            return null;
        }

        _logger.Info($"[Payout] Seller {sellerId} is eligible for payout. NetAmount: {payout.NetAmount:N0}.");
        return CreatePayoutListResponseFromPayout(payout);
    }

    public async Task<bool> ProcessSellerPayoutAsync(Guid sellerId)
    {
        // Check if there is any PROCESSING payout not completed yet
        var processingPayout = await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.PROCESSING)
            .FirstOrDefaultAsync();

        if (processingPayout != null)
        {
            _logger.Warn(
                $"[Payout] Seller {sellerId} has a payout in PROCESSING (Id: {processingPayout.Id}). Must wait until it is completed before processing another payout.");
            return false;
        }

        var payout = await GetEligiblePayoutForSellerAsync(sellerId);
        if (payout == null)
        {
            _logger.Warn($"[Payout] Seller {sellerId} is not eligible for payout.");
            return false;
        }

        var seller = await GetSellerWithStripeAccountAsync(sellerId);
        if (seller?.StripeAccountId == null)
        {
            _logger.Warn($"[Payout] Seller {sellerId} does not have a Stripe account.");
            return false;
        }

        try
        {
            await ExecuteStripePayoutAsync(payout, seller);
            await CreatePayoutLogAsync(payout.Id, PayoutStatus.REQUESTED, PayoutStatus.PROCESSING, "SELLER_REQUEST",
                "Seller requested payout.");
            await NotifySellerPayoutProcessingAsync(seller, payout);

            _logger.Success($"[Payout] Seller {sellerId} payout {payout.Id} moved to PROCESSING.");
            return true;
        }
        catch (Exception ex)
        {
            await HandlePayoutFailureAsync(payout, ex);
            return false;
        }
    }

    public async Task<PayoutCalculationResultDto> GetUpcomingPayoutForCurrentSellerAsync()
    {
        var seller = await GetCurrentSellerAsync();
        var hasRequestedPayout = await GetRequestedPayoutAsync(seller.Id);
        var pendingPayout = await GetPendingPayoutWithDetailsAsync(seller.Id);

        if (pendingPayout == null)
            return null;

        var orderDetailSummaries = CreateOrderDetailSummaries(pendingPayout);
        var payoutEligibility = await GetPayoutEligibilityAsync(seller, pendingPayout, hasRequestedPayout);

        return new PayoutCalculationResultDto
        {
            SellerId = seller.Id,
            SellerName = seller.CompanyName ?? seller.User?.FullName ?? "",
            SellerEmail = seller.User?.Email ?? "",
            StripeAccountId = seller.StripeAccountId,
            GrossAmount = pendingPayout.GrossAmount,
            PlatformFeeRate = pendingPayout.PlatformFeeRate,
            PlatformFeeAmount = pendingPayout.PlatformFeeAmount,
            NetAmount = pendingPayout.NetAmount,
            TotalOrderDetails = orderDetailSummaries.Count,
            TotalOrders = orderDetailSummaries.Select(x => x.OrderId).Distinct().Count(),
            CanPayout = payoutEligibility.CanPayout,
            PayoutBlockReason = payoutEligibility.BlockReason,
            OrderDetailSummaries = orderDetailSummaries
        };
    }

    public async Task<List<PayoutListResponseDto>> GetSellerPayoutsForPeriodAsync(PayoutCalculationRequestDto req)
    {
        var seller = await GetCurrentSellerAsync();
        var payouts = await GetPayoutsInPeriodAsync(seller.Id, req.PeriodStart, req.PeriodEnd);

        return payouts.Select(p => CreatePayoutListResponse(p, seller)).ToList();
    }

    public async Task<PayoutDetailResponseDto?> GetPayoutDetailByIdAsync(Guid payoutId)
    {
        var payout = await GetPayoutWithFullDetailsAsync(payoutId);
        if (payout == null)
            return null;

        return CreatePayoutDetailResponse(payout);
    }

    public async Task<PayoutDetailResponseDto?> RequestPayoutAsync(Guid sellerId)
    {
        await ValidatePayoutRequestAsync(sellerId);

        var seller = await GetSellerAsync(sellerId);
        var payout = await GetPendingPayoutWithDetailsAsync(sellerId);

        if (payout == null)
        {
            _logger.Warn($"[Payout] Seller {sellerId} không có payout nào ở trạng thái PENDING.");
            return null;
        }

        await ValidatePayoutEligibilityAsync(seller, payout);
        await UpdatePayoutToRequestedAsync(payout);

        _logger.Success($"[Payout] Seller {sellerId} đã gửi yêu cầu rút tiền cho payout {payout.Id}.");
        return await GetPayoutDetailByIdAsync(payout.Id);
    }

    public async Task<MemoryStream> ExportLatestPayoutProofAsync()
    {
        var seller = await GetCurrentSellerAsync();
        var payout = await GetLatestProcessingPayoutAsync(seller.Id);

        if (payout == null)
            throw ErrorHelper.BadRequest("Not found the newest handling payout to show");

        return GeneratePayoutExcel(new List<Payout> { payout }, seller);
    }

    public async Task<MemoryStream> ExportPayoutByIdAsync(Guid payoutId)
    {
        var seller = await GetCurrentSellerAsync();
        var payout = await GetPayoutForSellerAsync(payoutId, seller.Id);

        if (payout == null)
            throw new InvalidOperationException("Payout not found.");

        return GeneratePayoutExcel(new List<Payout> { payout }, seller);
    }

    public async Task<Pagination<PayoutListResponseDto>> GetPayoutsForAdminAsync(PayoutAdminQueryParameter param)
    {
        return await GetPayoutsPaginatedAsync(param);
    }

    public async Task<Pagination<PayoutListResponseDto>> GetPayoutsForCurrentSellerAsync(
        PayoutAdminQueryParameter param)
    {
        var seller = await GetCurrentSellerAsync();
        return await GetPayoutsPaginatedAsync(param, seller.Id);
    }

    public async Task<PayoutDetailResponseDto?> AdminConfirmPayoutWithProofAsync(Guid payoutId, List<IFormFile> files,
        Guid adminUserId)
    {
        ValidateProofImages(files);

        var payout = await GetPayoutForConfirmationAsync(payoutId);
        var uploadedUrls = await UploadProofImagesAsync(payoutId, files);

        await CompletePayoutAsync(payout, uploadedUrls, adminUserId);
        await NotifySellerPayoutCompletedAsync(payout);

        _logger.Success($"[Payout] Admin confirmed payout {payout.Id} for seller {payout.SellerId}.");
        return await GetPayoutDetailByIdAsync(payout.Id);
    }

    #endregion

    #region Private Helper Methods

    private bool IsOrderEligibleForPayout(Order order)
    {
        return order != null && order.Status == OrderStatus.COMPLETED.ToString();
    }


    private async Task<Payout> GetOrCreatePendingPayoutAsync(Guid sellerId, CancellationToken? ct, Order order)
    {
        var cancellationToken = ct ?? CancellationToken.None;

        // Normalize order completed date to UTC date-only
        DateTime orderCompletedDateUtc;
        if (order.CompletedAt.HasValue)
        {
            orderCompletedDateUtc = DateTime.SpecifyKind(order.CompletedAt.Value, DateTimeKind.Utc).Date;
        }
        else
        {
            // Nếu CompletedAt null => fallback, nhưng nên log để dễ debug
            orderCompletedDateUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            _logger.Warn(
                $"[Payout] Order {order.Id} has no CompletedAt. Using UTC today {orderCompletedDateUtc:yyyy-MM-dd} as fallback.");
        }

        var orderMonday = GetMondayOfWeek(orderCompletedDateUtc); // Monday 00:00 UTC
        var orderNextMonday = DateTime.SpecifyKind(orderMonday.AddDays(7), DateTimeKind.Utc);

        // 1) Try get existing pending payout (one per seller)
        var payout = await _unitOfWork.Payouts.GetQueryable()
            .FirstOrDefaultAsync(p => p.SellerId == sellerId && p.Status == PayoutStatus.PENDING, cancellationToken);

        if (payout == null)
        {
            // Create new payout — wrap create into try/catch to handle race condition (unique index)
            var newPayout = new Payout
            {
                SellerId = sellerId,
                PeriodStart = orderMonday,
                PeriodEnd = orderNextMonday,
                PeriodType = PayoutPeriodType.WEEKLY,
                Status = PayoutStatus.PENDING,
                GrossAmount = 0m,
                PlatformFeeRate = PLATFORM_FEE_RATE,
                PlatformFeeAmount = 0m,
                NetAmount = 0m
            };

            try
            {
                newPayout = await _unitOfWork.Payouts.AddAsync(newPayout);
                await _unitOfWork.SaveChangesAsync();

                _logger.Info(
                    $"[Payout] Created new pending payout for seller {sellerId} for period {orderMonday:yyyy-MM-dd} - {orderNextMonday:yyyy-MM-dd}.");
                return newPayout;
            }
            catch (DbUpdateException ex)
            {
                // Có thể là do race -> một pending được tạo đồng thời, nên re-query
                _logger.Warn(
                    $"[Payout] Concurrency create conflict for seller {sellerId}. Re-query pending payout. Error: {ex.Message}");
                payout = await _unitOfWork.Payouts.GetQueryable()
                    .FirstOrDefaultAsync(p => p.SellerId == sellerId && p.Status == PayoutStatus.PENDING,
                        cancellationToken);

                if (payout == null) throw; // nếu vẫn null, rethrow để surface lỗi
            }
        }

        // 2) If we have a pending payout, ensure period covers the order's week
        // Normalize existing period datetimes to UTC kind/date-only for safe comparison
        var existingStart = DateTime.SpecifyKind(payout.PeriodStart.Date, DateTimeKind.Utc);
        var existingEnd = DateTime.SpecifyKind(payout.PeriodEnd.Date, DateTimeKind.Utc);

        var newStart = orderMonday < existingStart ? orderMonday : existingStart;
        var newEnd = orderNextMonday > existingEnd ? orderNextMonday : existingEnd;

        if (newStart != existingStart || newEnd != existingEnd)
        {
            payout.PeriodStart = newStart;
            payout.PeriodEnd = newEnd;

            await _unitOfWork.Payouts.Update(payout);
            await _unitOfWork.SaveChangesAsync();

            _logger.Info(
                $"[Payout] Updated payout {payout.Id} period to {newStart:yyyy-MM-dd} - {newEnd:yyyy-MM-dd} for seller {sellerId}.");
        }

        return payout;
    }


    private DateTime GetMondayOfWeek(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysToSubtract = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysToSubtract);
    }

    private async Task AddOrderDetailsToPayoutAsync(Payout payout, Order order, CancellationToken? ct)
    {
        foreach (var od in order.OrderDetails.Where(od => od.Status != OrderDetailItemStatus.CANCELLED))
        {
            if (await IsOrderDetailAlreadyInPayoutAsync(payout.Id, od.Id, ct))
            {
                _logger.Warn($"[Payout] OrderDetail {od.Id} already included in payout {payout.Id}. Skipping.");
                continue;
            }

            var payoutDetail = CreatePayoutDetail(payout.Id, od);
            await _unitOfWork.PayoutDetails.AddAsync(payoutDetail);
            payout.GrossAmount += payoutDetail.ContributedAmount;

            _logger.Info($"[Payout] Added OrderDetail {od.Id} to payout {payout.Id}.");
        }
    }

    private async Task<bool> IsOrderDetailAlreadyInPayoutAsync(Guid payoutId, Guid orderDetailId, CancellationToken? ct)
    {
        return await _unitOfWork.PayoutDetails.GetQueryable()
            .AnyAsync(pd => pd.PayoutId == payoutId && pd.OrderDetailId == orderDetailId, ct ?? CancellationToken.None);
    }

    private PayoutDetail CreatePayoutDetail(Guid payoutId, OrderDetail od)
    {
        return new PayoutDetail
        {
            PayoutId = payoutId,
            OrderDetailId = od.Id,
            OrderId = od.OrderId,
            OriginalAmount = od.TotalPrice,
            DiscountAmount = od.DetailDiscountPromotion ?? 0,
            FinalAmount = od.FinalDetailPrice ?? od.TotalPrice,
            RefundAmount = 0,
            ContributedAmount = od.FinalDetailPrice ?? od.TotalPrice,
            CalculatedAt = DateTime.UtcNow
        };
    }

    private async Task UpdatePayoutAmountsAsync(Payout payout, Order order)
    {
        payout.PlatformFeeAmount = Math.Round(payout.GrossAmount * payout.PlatformFeeRate / 100m, 2);
        payout.NetAmount = payout.GrossAmount - payout.PlatformFeeAmount;
        order.PayoutId = payout.Id;

        await _unitOfWork.Payouts.Update(payout);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<bool> IsSellerEligibleForPayoutAsync(Guid sellerId)
    {
        var lastPayout = await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.SellerId == sellerId &&
                        (p.Status == PayoutStatus.COMPLETED || p.Status == PayoutStatus.PROCESSING))
            .OrderByDescending(p => p.CompletedAt ?? p.ProcessedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastPayout != null)
        {
            var lastDate = lastPayout.CompletedAt ?? lastPayout.ProcessedAt ?? lastPayout.CreatedAt;
            if ((DateTime.UtcNow - lastDate).TotalDays < MINIMUM_DAYS_BETWEEN_PAYOUTS)
            {
                _logger.Warn(
                    $"[Payout] Seller {sellerId} must wait {MINIMUM_DAYS_BETWEEN_PAYOUTS} days between payouts. Last payout: {lastDate:yyyy-MM-dd}.");
                return false;
            }
        }

        return true;
    }

    private async Task<Payout?> GetMainActivePayoutWithDetailsAsync(Guid sellerId)
    {
        // 1. Ưu tiên PROCESSING payout (đã được xử lý bởi Stripe)
        var processingPayout = await _unitOfWork.Payouts.GetQueryable()
            .Include(p => p.PayoutDetails).ThenInclude(o => o.OrderDetail).ThenInclude(o => o.Order)
            .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.PROCESSING)
            .OrderByDescending(p => p.ProcessedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync();

        if (processingPayout != null)
        {
            _logger.Info($"[Payout] Found PROCESSING payout {processingPayout.Id} for seller {sellerId}");
            return processingPayout;
        }

        // 2. Tiếp theo là REQUESTED payout (seller đã yêu cầu nhưng chưa xử lý)
        var requestedPayout = await _unitOfWork.Payouts.GetQueryable()
            .Include(p => p.PayoutDetails).ThenInclude(o => o.OrderDetail).ThenInclude(o => o.Order)
            .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.REQUESTED)
            .OrderByDescending(p => p.ProcessedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync();

        if (requestedPayout != null)
        {
            _logger.Info($"[Payout] Found REQUESTED payout {requestedPayout.Id} for seller {sellerId}");
            return requestedPayout;
        }

        // 3. Cuối cùng mới là PENDING payout
        var pendingPayout = await _unitOfWork.Payouts.GetQueryable()
            .Include(p => p.PayoutDetails).ThenInclude(o => o.OrderDetail).ThenInclude(o => o.Order)
            .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.PENDING)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (pendingPayout != null)
            _logger.Info($"[Payout] Found PENDING payout {pendingPayout.Id} for seller {sellerId}");

        return pendingPayout;
    }

    private async Task<Seller?> GetSellerWithStripeAccountAsync(Guid sellerId)
    {
        return await _unitOfWork.Sellers.GetByIdAsync(sellerId);
    }

    private async Task ExecuteStripePayoutAsync(Payout payout, Seller seller)
    {
        var rate = await _currencyConversionService.GetVNDToUSDRate();
        if (rate == null || rate <= 0)
            throw new InvalidOperationException("Currency conversion rate is invalid.");

        var usdAmount = payout.NetAmount / rate.Value * 100;

        var transfer = await _stripeService.PayoutToSellerAsync(payout.Id,
            seller.StripeAccountId,
            usdAmount,
            "usd",
            $"Payout for seller {seller.Id} - period {payout.PeriodStart:yyyy-MM-dd} to {payout.PeriodEnd:yyyy-MM-dd}");

        payout.Status = PayoutStatus.PROCESSING;
        payout.ProcessedAt = DateTime.UtcNow;
        payout.StripeTransferId = transfer.Id;
        payout.StripeDestinationAccount = seller.StripeAccountId;

        await _unitOfWork.Payouts.Update(payout);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task HandlePayoutFailureAsync(Payout payout, Exception ex)
    {
        payout.Status = PayoutStatus.FAILED;
        await _unitOfWork.Payouts.Update(payout);

        await CreatePayoutLogAsync(payout.Id, PayoutStatus.REQUESTED, PayoutStatus.FAILED,
            "SELLER_REQUEST", "Stripe payout failed.", ex.Message);

        await _unitOfWork.SaveChangesAsync();
        _logger.Warn($"[Payout] Stripe payout failed for seller {payout.SellerId}: {ex.Message}");
    }

    private async Task CreatePayoutLogAsync(Guid payoutId, PayoutStatus fromStatus, PayoutStatus toStatus,
        string action, string details, string errorMessage = null)
    {
        var log = new PayoutLog
        {
            PayoutId = payoutId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Action = action,
            Details = details,
            TriggeredByUserId = _claimsService.CurrentUserId == Guid.Empty ? null : _claimsService.CurrentUserId,
            LoggedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };

        await _unitOfWork.PayoutLogs.AddAsync(log);
    }

    private async Task<Seller> GetCurrentSellerAsync()
    {
        var userId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.GetQueryable()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (seller == null)
            throw ErrorHelper.BadRequest("Seller profile not found.");

        return seller;
    }

    private async Task<Seller> GetSellerAsync(Guid sellerId)
    {
        var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId);
        if (seller == null)
            throw ErrorHelper.BadRequest($"Seller {sellerId} không tồn tại.");
        return seller;
    }

    private async Task<Payout?> GetRequestedPayoutAsync(Guid sellerId)
    {
        return await _unitOfWork.Payouts.GetQueryable()
            .FirstOrDefaultAsync(p => p.SellerId == sellerId && p.Status == PayoutStatus.REQUESTED);
    }

    private async Task<Payout?> GetProcessingPayoutAsync(Guid sellerId)
    {
        // Check if there is any PROCESSING payout not completed yet
        var processingPayout = await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.SellerId == sellerId && p.Status == PayoutStatus.PROCESSING)
            .FirstOrDefaultAsync();

        if (processingPayout != null)
            _logger.Warn(
                $"[Payout] Seller {sellerId} has a payout in PROCESSING (Id: {processingPayout.Id}). Must wait until it is completed before processing another payout.");

        return processingPayout;
    }

    private async Task<Payout?> GetPendingPayoutWithDetailsAsync(Guid sellerId)
    {
        return await _unitOfWork.Payouts.GetQueryable().AsNoTracking()
            .Include(p => p.PayoutDetails).ThenInclude(o => o.OrderDetail).ThenInclude(o => o.Order)
            .FirstOrDefaultAsync(p => p.SellerId == sellerId && p.Status == PayoutStatus.PENDING);
    }

    private List<PayoutDetailSummaryDto> CreateOrderDetailSummaries(Payout payout)
    {
        return payout.PayoutDetails.Select(pd => new PayoutDetailSummaryDto
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
    }

    private async Task<(bool CanPayout, string BlockReason)> GetPayoutEligibilityAsync(Seller seller, Payout payout,
        Payout hasRequestedPayout)
    {
        if (hasRequestedPayout != null)
            return (false, $"Bạn đang có một yêu cầu rút tiền chưa được duyệt. PayoutId {hasRequestedPayout.Id}");

        if (seller.StripeAccountId == null)
            return (false, "Seller chưa liên kết Stripe account.");

        if (payout.NetAmount < MINIMUM_PAYOUT_AMOUNT)
            return (false, "Số tiền chưa đủ tối thiểu để rút.");

        // Check if there is any PROCESSING payout not completed yet
        var processingPayout = await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.SellerId == seller.Id && p.Status == PayoutStatus.PROCESSING)
            .FirstOrDefaultAsync();

        if (processingPayout != null)
        {
            _logger.Warn(
                $"[Payout] Seller {seller.Id} has a payout in PROCESSING (Id: {processingPayout.Id}). Must wait until it is completed before processing another payout.");
            return (false,
                $"[Payout] Seller {seller.Id} has a payout in PROCESSING (Id: {processingPayout.Id}). Must wait until it is completed before processing another payout.");
        }

        // TODO: Uncomment this block when ready for production
        /*
        // Check 7-day waiting period
        var lastPayout = await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.SellerId == seller.Id &&
                        (p.Status == PayoutStatus.COMPLETED || p.Status == PayoutStatus.PROCESSING))
            .OrderByDescending(p => p.CompletedAt ?? p.ProcessedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastPayout != null)
        {
            var lastDate = lastPayout.CompletedAt ?? lastPayout.ProcessedAt ?? lastPayout.CreatedAt;
            var daysSinceLast = (DateTime.UtcNow - lastDate).TotalDays;
            if (daysSinceLast < MINIMUM_DAYS_BETWEEN_PAYOUTS)
            {
                return (false, $"Bạn phải chờ đủ {MINIMUM_DAYS_BETWEEN_PAYOUTS} ngày kể từ lần rút tiền gần nhất ({lastDate:yyyy-MM-dd}).");
            }
        }
        */

        return (true, null);
    }

    private async Task<List<Payout>> GetPayoutsInPeriodAsync(Guid sellerId, DateTime periodStart, DateTime periodEnd)
    {
        return await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.SellerId == sellerId &&
                        p.PeriodStart >= periodStart.Date &&
                        p.PeriodEnd <= periodEnd.Date)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    private PayoutListResponseDto CreatePayoutListResponse(Payout p, Seller seller)
    {
        return new PayoutListResponseDto
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
        };
    }

    private async Task<Payout?> GetPayoutWithFullDetailsAsync(Guid payoutId)
    {
        return await _unitOfWork.Payouts.GetQueryable()
            .Include(p => p.PayoutDetails).ThenInclude(o => o.OrderDetail).ThenInclude(o => o.Order)
            .Include(p => p.PayoutLogs)
            .Include(p => p.Seller).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(p => p.Id == payoutId);
    }

    private PayoutDetailResponseDto CreatePayoutDetailResponse(Payout payout)
    {
        var seller = payout.Seller;
        var payoutDetails = CreateOrderDetailSummaries(payout);
        var payoutLogs = CreatePayoutLogDtos(payout.PayoutLogs);

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

    private List<PayoutLogDto> CreatePayoutLogDtos(ICollection<PayoutLog> payoutLogs)
    {
        return payoutLogs.Select(log => new PayoutLogDto
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
    }

    private async Task ValidatePayoutRequestAsync(Guid sellerId)
    {
        var hasRequestedPayout = await GetRequestedPayoutAsync(sellerId);
        if (hasRequestedPayout != null)
            throw ErrorHelper.BadRequest(
                $"[Payout] Seller {sellerId} đã có một yêu cầu rút tiền chưa được duyệt. PayoutId {hasRequestedPayout.Id}");
    }

    private async Task ValidatePayoutEligibilityAsync(Seller seller, Payout payout)
    {
        var canPayout = seller.StripeAccountId != null && payout.NetAmount >= MINIMUM_PAYOUT_AMOUNT;

        if (!canPayout)
        {
            var reason = seller.StripeAccountId == null
                ? "Seller chưa liên kết Stripe account."
                : "Số tiền chưa đủ tối thiểu để rút.";

            _logger.Warn($"[Payout] Seller {seller.Id} không đủ điều kiện rút tiền: {reason}");
            throw ErrorHelper.BadRequest($"[Payout] {reason}");
        }
    }

    private async Task UpdatePayoutToRequestedAsync(Payout payout)
    {
        var now = DateTime.UtcNow;
        //payout.PeriodEnd = now.Date.AddDays(7 - (int)now.DayOfWeek - 1);
        payout.Status = PayoutStatus.REQUESTED;
        payout.ProcessedAt = now;

        await _unitOfWork.Payouts.Update(payout);

        await CreatePayoutLogAsync(payout.Id, PayoutStatus.PENDING, PayoutStatus.REQUESTED,
            "SELLER_REQUEST", "Seller gửi yêu cầu rút tiền.");

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Payout?> GetLatestProcessingPayoutAsync(Guid sellerId)
    {
        return await _unitOfWork.Payouts.GetQueryable()
            .Include(p => p.PayoutDetails).ThenInclude(pd => pd.OrderDetail).ThenInclude(o => o.Product)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(p =>
                p.SellerId == sellerId && (p.Status == PayoutStatus.PROCESSING || p.Status == PayoutStatus.COMPLETED));
    }

    private async Task<Payout?> GetPayoutForSellerAsync(Guid payoutId, Guid sellerId)
    {
        return await _unitOfWork.Payouts.GetQueryable()
            .Include(p => p.PayoutDetails).ThenInclude(pd => pd.OrderDetail)
            .FirstOrDefaultAsync(p => p.Id == payoutId && p.SellerId == sellerId);
    }

    private async Task<Pagination<PayoutListResponseDto>> GetPayoutsPaginatedAsync(PayoutAdminQueryParameter param,
        Guid? sellerId = null)
    {
        var query = BuildPayoutQuery(param, sellerId);
        var totalCount = await query.CountAsync();

        var payouts = param.PageIndex == 0
            ? await query.ToListAsync()
            : await query.Skip((param.PageIndex - 1) * param.PageSize).Take(param.PageSize).ToListAsync();

        var items = payouts.Select(p => CreatePayoutListResponseFromPayout(p)).ToList();
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

    private PayoutListResponseDto CreatePayoutListResponseFromPayout(Payout p)
    {
        return new PayoutListResponseDto
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
        };
    }

    private void ValidateProofImages(List<IFormFile> files)
    {
        if (files == null || files.Count == 0 || files.All(f => f.Length == 0))
            throw ErrorHelper.BadRequest("No valid proof images provided.");
    }

    private async Task<Payout> GetPayoutForConfirmationAsync(Guid payoutId)
    {
        var payout = await _unitOfWork.Payouts.GetQueryable()
            .Include(p => p.Seller).ThenInclude(s => s.User)
            .Include(p => p.PayoutLogs)
            .FirstOrDefaultAsync(p => p.Id == payoutId);

        if (payout == null)
            throw ErrorHelper.NotFound("Payout not found.");

        //if (payout.Status != PayoutStatus.PROCESSING  ||  payout.Status != )
        //    throw ErrorHelper.BadRequest("Only payouts in PENDING OR COMPLETED status can be confirmed.");

        return payout;
    }

    private async Task<List<string>> UploadProofImagesAsync(Guid payoutId, List<IFormFile> files)
    {
        var uploadedUrls = new List<string>();
        foreach (var file in files.Where(f => f.Length > 0).Take(MAX_PROOF_IMAGES))
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

    private async Task CompletePayoutAsync(Payout payout, List<string> uploadedUrls, Guid adminUserId)
    {
        payout.ProofImageUrls = uploadedUrls;
        payout.Status = PayoutStatus.COMPLETED;
        payout.CompletedAt = DateTime.UtcNow;

        await _unitOfWork.Payouts.Update(payout);

        await CreatePayoutLogWithTriggerUserAsync(payout.Id, PayoutStatus.PROCESSING, PayoutStatus.COMPLETED,
            "ADMIN_CONFIRM", $"Admin confirmed payout and uploaded {uploadedUrls.Count} proof images.", null,
            adminUserId);

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task CreatePayoutLogWithTriggerUserAsync(Guid payoutId, PayoutStatus fromStatus,
        PayoutStatus toStatus,
        string action, string details, string errorMessage = null, Guid? triggeredByUserId = null)
    {
        var log = new PayoutLog
        {
            PayoutId = payoutId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Action = action,
            Details = details,
            TriggeredByUserId = triggeredByUserId ??
                                (_claimsService.CurrentUserId == Guid.Empty ? null : _claimsService.CurrentUserId),
            LoggedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };

        await _unitOfWork.PayoutLogs.AddAsync(log);
    }

    private async Task NotifySellerPayoutProcessingAsync(Seller seller, Payout payout)
    {
        var notificationDto = new NotificationDto
        {
            Title = "Payout is being processed",
            Message =
                $"Your payout for period {payout.PeriodStart:yyyy-MM-dd} to {payout.PeriodEnd:yyyy-MM-dd} is being processed. Amount: {payout.NetAmount:N0} VND.",
            Type = NotificationType.System,
            SourceUrl = null
        };
        await _notificationService.PushNotificationToUser(seller.UserId, notificationDto);
    }

    private async Task NotifySellerPayoutCompletedAsync(Payout payout)
    {
        if (payout.Seller?.User != null)
        {
            var notificationDto = new NotificationDto
            {
                Title = "Payout Completed",
                Message =
                    $"Your payout for period {payout.PeriodStart:yyyy-MM-dd} to {payout.PeriodEnd:yyyy-MM-dd} has been completed. Please check your account.",
                Type = NotificationType.System,
                SourceUrl = null
            };
            await _notificationService.PushNotificationToUser(payout.Seller.User.Id, notificationDto);
        }
    }

    #endregion

    #region Excel Generation

    private MemoryStream GeneratePayoutExcel(List<Payout> payouts, Seller seller)
    {
        ExcelPackage.License.SetNonCommercialPersonal("your-name-or-organization");
        var package = new ExcelPackage();

        CreatePayoutSummarySheet(package, payouts, seller);
        CreatePayoutDetailsSheet(package, payouts);

        var stream = new MemoryStream();
        package.SaveAs(stream);
        if (stream.CanSeek) stream.Position = 0;
        return stream;
    }

    private void CreatePayoutSummarySheet(ExcelPackage package, List<Payout> payouts, Seller seller)
    {
        var ws = package.Workbook.Worksheets.Add("Payouts");
        var headers = new[]
        {
            "SellerName", "SellerEmail", "StripeAccountId", "PeriodStart", "PeriodEnd", "GrossAmount",
            "PlatformFeeAmount", "NetAmount", "Status", "CreatedAt", "ProcessedAt", "CompletedAt",
            "StripeTransferId", "FailureReason"
        };

        // Create headers
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // Fill data
        for (var i = 0; i < payouts.Count; i++)
        {
            var p = payouts[i];
            var row = i + 2;

            ws.Cells[row, 1].Value = seller.CompanyName ?? seller.User?.FullName ?? "";
            ws.Cells[row, 2].Value = seller.User?.Email ?? "";
            ws.Cells[row, 3].Value = seller.StripeAccountId ?? "";
            ws.Cells[row, 4].Value = p.PeriodStart.ToString("yyyy-MM-dd");
            ws.Cells[row, 5].Value = p.PeriodEnd.ToString("yyyy-MM-dd");
            ws.Cells[row, 6].Value = p.GrossAmount;
            ws.Cells[row, 7].Value = p.PlatformFeeAmount;
            ws.Cells[row, 8].Value = p.NetAmount;
            ws.Cells[row, 9].Value = p.Status.ToString();
            ws.Cells[row, 10].Value = p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cells[row, 11].Value = p.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            ws.Cells[row, 12].Value = p.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            ws.Cells[row, 13].Value = p.StripeTransferId ?? "";
            ws.Cells[row, 14].Value = p.FailureReason ?? "";
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
    }

    private void CreatePayoutDetailsSheet(ExcelPackage package, List<Payout> payouts)
    {
        var wsDetail = package.Workbook.Worksheets.Add("Payout Details");
        var detailHeaders = new[]
        {
            "PayoutId", "OrderDetailId", "OrderId", "ProductName", "Quantity", "OriginalAmount",
            "DiscountAmount", "FinalAmount", "RefundAmount", "ContributedAmount", "OrderCompletedAt"
        };

        // Create headers
        for (var i = 0; i < detailHeaders.Length; i++)
        {
            var cell = wsDetail.Cells[1, i + 1];
            cell.Value = detailHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // Fill data
        var row = 2;
        foreach (var p in payouts)
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

        wsDetail.Cells[wsDetail.Dimension.Address].AutoFitColumns();
    }

    #endregion
}