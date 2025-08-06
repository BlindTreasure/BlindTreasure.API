using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BlindTreasure.Application.Services;

/// <summary>
/// DTO dùng cho thống kê order detail, thay thế anonymous type.
/// </summary>
public class OrderDetailStatisticsItem
{
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? DetailDiscountPromotion { get; set; }
    public decimal? FinalDetailPrice { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductImageUrl { get; set; }
    public decimal ProductPrice { get; set; }
    public Guid? BlindBoxId { get; set; }
    public string? BlindBoxName { get; set; }
    public string? BlindBoxImageUrl { get; set; }
    public decimal BlindBoxPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

public class SellerStatisticsService : ISellerStatisticsService
{
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;

    public SellerStatisticsService(ILoggerService loggerService, IUnitOfWork unitOfWork)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Orchestration method: Tổng hợp tất cả các thống kê cho Seller Dashboard.
    /// </summary>
    public async Task<SellerDashboardStatisticsDto> GetDashboardStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        _loggerService.Info($"[SellerStatistics] Start dashboard statistics for seller {sellerId}");

        // Xác định khoảng thời gian thống kê dựa trên request
        var (start, end) = GetStatisticsDateRange(req);
        _loggerService.Info($"[SellerStatistics] Date range: {start:O} - {end:O}");

        // Truy vấn orderDetails đã filter, chỉ lấy các trường cần thiết
        _loggerService.Info("[SellerStatistics] Querying order details for statistics...");
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        _loggerService.Info($"[SellerStatistics] Fetched {orderDetails.Count} order details.");

        // Tổng hợp các module thống kê cho dashboard
        var overview = await BuildOverviewStatisticsAsync(sellerId, req, start, end, ct, orderDetails);
        _loggerService.Info("[SellerStatistics] Overview statistics built.");

        var topProducts = BuildTopProducts(orderDetails);
        _loggerService.Info("[SellerStatistics] Top selling products calculated.");

        var topBlindBoxes = BuildTopBlindBoxes(orderDetails);
        _loggerService.Info("[SellerStatistics] Top selling blindboxes calculated.");

        var orderStatusStats = BuildOrderStatusStatistics(orderDetails);
        _loggerService.Info("[SellerStatistics] Order status statistics calculated.");

        var timeSeries = BuildTimeSeriesData(orderDetails, req.Range, start, end);
        _loggerService.Info("[SellerStatistics] Time series statistics calculated.");

        overview.TimeSeriesData = timeSeries;

        _loggerService.Success($"[SellerStatistics] Dashboard statistics built for seller {sellerId}");

        return new SellerDashboardStatisticsDto
        {
            Overview = overview,
            TopProducts = topProducts,
            TopBlindBoxes = topBlindBoxes,
            OrderStatusStats = orderStatusStats,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Tính toán các chỉ số tổng quan: doanh thu, đơn hàng, sản phẩm bán, AOV, growth.
    /// </summary>
    private async Task<SellerOverviewStatisticsDto> BuildOverviewStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        DateTime start,
        DateTime end,
        CancellationToken ct,
        List<OrderDetailStatisticsItem> orderDetails)
    {
        // Lấy danh sách orderId duy nhất
        var orderIds = orderDetails.Select(od => od.OrderId).Distinct().ToList();
        var totalOrders = orderIds.Count;
        var totalProductsSold = orderDetails.Sum(od => od.Quantity);

        // Doanh thu gộp (chưa trừ giảm giá)
        var grossRevenue = orderDetails.Sum(od => od.TotalPrice);

        // Tổng giảm giá từ từng OrderDetail
        var totalDiscount = orderDetails.Sum(od => od.DetailDiscountPromotion ?? 0m);

        // Doanh thu thực nhận (đã trừ giảm giá từng item)
        var netRevenue =
            orderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice - (od.DetailDiscountPromotion ?? 0m));

        // Tổng tiền refund từ các payment liên quan
        var totalRefunded = await _unitOfWork.Payments.GetQueryable()
            .Where(p => orderIds.Contains(p.OrderId))
            .SumAsync(p => (decimal?)p.RefundedAmount) ?? 0m;

        netRevenue -= totalRefunded;

        var averageOrderValue = totalOrders > 0 ? Math.Round(netRevenue / totalOrders, 2) : 0m;

        _loggerService.Info(
            $"[SellerStatistics] GrossRevenue={grossRevenue}, NetRevenue={netRevenue}, TotalDiscount={totalDiscount}, TotalRefunded={totalRefunded}");

        // Lấy dữ liệu kỳ trước để tính growth
        var (lastStart, lastEnd) = GetPreviousDateRange(req.Range, start, end);
        var lastOrderDetails = await GetOrderDetailsAsync(sellerId, lastStart, lastEnd, ct);

        var lastOrderIds = lastOrderDetails.Select(od => od.OrderId).Distinct().ToList();
        var lastOrders = lastOrderIds.Count;
        var lastProductsSold = lastOrderDetails.Sum(od => od.Quantity);
        var lastRevenue =
            lastOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice - (od.DetailDiscountPromotion ?? 0m));
        var lastAOV = lastOrders > 0 ? Math.Round(lastRevenue / lastOrders, 2) : 0m;

        // Tính phần trăm tăng trưởng so với kỳ trước
        var revenueGrowth = lastRevenue > 0 ? Math.Round((netRevenue - lastRevenue) * 100 / lastRevenue, 2) : 0m;
        var ordersGrowth = lastOrders > 0 ? Math.Round((decimal)(totalOrders - lastOrders) * 100 / lastOrders, 2) : 0m;
        var productsGrowth = lastProductsSold > 0
            ? Math.Round((decimal)(totalProductsSold - lastProductsSold) * 100 / lastProductsSold, 2)
            : 0m;
        var aovGrowth = lastAOV > 0 ? Math.Round((averageOrderValue - lastAOV) * 100 / lastAOV, 2) : 0m;

        _loggerService.Info(
            $"[SellerStatistics] Growth: Revenue={revenueGrowth}%, Orders={ordersGrowth}%, Products={productsGrowth}%, AOV={aovGrowth}%");

        return new SellerOverviewStatisticsDto
        {
            TotalRevenue = decimal.Round(netRevenue, 2),
            TotalRevenueLastPeriod = decimal.Round(lastRevenue, 2),
            RevenueGrowthPercent = revenueGrowth,
            TotalOrders = totalOrders,
            TotalOrdersLastPeriod = lastOrders,
            OrdersGrowthPercent = ordersGrowth,
            TotalProductsSold = totalProductsSold,
            TotalProductsSoldLastPeriod = lastProductsSold,
            ProductsSoldGrowthPercent = productsGrowth,
            AverageOrderValue = averageOrderValue,
            AverageOrderValueLastPeriod = lastAOV,
            AverageOrderValueGrowthPercent = aovGrowth
        };
    }

    /// <summary>
    /// Trả về top 5 sản phẩm bán chạy nhất (theo số lượng).
    /// </summary>
    private List<TopSellingProductDto> BuildTopProducts(List<OrderDetailStatisticsItem> orderDetails)
    {
        // Nhóm theo ProductId, tính tổng số lượng và doanh thu thực nhận
        var result = orderDetails
            .Where(od => od.ProductId != null)
            .GroupBy(od => od.ProductId)
            .Select(g => new TopSellingProductDto
            {
                ProductId = g.Key!.Value,
                ProductName = g.First().ProductName ?? string.Empty,
                ProductImageUrl = g.First().ProductImageUrl ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice - (x.DetailDiscountPromotion ?? 0m)),

                Price = g.First().ProductPrice
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();

        _loggerService.Info($"[SellerStatistics] TopProducts: {string.Join(", ", result.Select(x => x.ProductName))}");
        return result;
    }

    /// <summary>
    /// Trả về top 5 blindbox bán chạy nhất (theo số lượng).
    /// </summary>
    private List<TopSellingBlindBoxDto> BuildTopBlindBoxes(List<OrderDetailStatisticsItem> orderDetails)
    {
        var result = orderDetails
            .Where(od => od.BlindBoxId != null)
            .GroupBy(od => od.BlindBoxId)
            .Select(g => new TopSellingBlindBoxDto
            {
                BlindBoxId = g.Key!.Value,
                BlindBoxName = g.First().BlindBoxName ?? string.Empty,
                BlindBoxImageUrl = g.First().BlindBoxImageUrl ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice - (x.DetailDiscountPromotion ?? 0m)),
                Price = g.First().BlindBoxPrice
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();

        _loggerService.Info(
            $"[SellerStatistics] TopBlindBoxes: {string.Join(", ", result.Select(x => x.BlindBoxName))}");
        return result;
    }

    /// <summary>
    /// Thống kê trạng thái đơn hàng (số lượng, doanh thu, phần trăm).
    /// </summary>
    private List<OrderStatusStatisticsDto> BuildOrderStatusStatistics(List<OrderDetailStatisticsItem> orderDetails)
    {
        var totalOrders = orderDetails.Select(od => od.OrderId).Distinct().Count();
        var result = orderDetails
            .GroupBy(od => od.Status)
            .Select(g => new OrderStatusStatisticsDto
            {
                Status = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice - (x.DetailDiscountPromotion ?? 0m)),
                Percentage = totalOrders > 0 ? Math.Round((decimal)g.Count() * 100 / totalOrders, 2) : 0m
            })
            .ToList();

        _loggerService.Info(
            $"[SellerStatistics] OrderStatusStats: {string.Join(", ", result.Select(x => $"{x.Status}:{x.Count}"))}");
        return result;
    }

    /// <summary>
    /// Thống kê theo thời gian (time series) cho dashboard.
    /// </summary>
    private SellerStatisticsResponseDto BuildTimeSeriesData(
        List<OrderDetailStatisticsItem> orderDetails,
        StatisticsTimeRange range,
        DateTime start,
        DateTime end)
    {
        var categories = new List<string>();
        var sales = new List<int>();
        var revenue = new List<decimal>();

        // Hàm lấy doanh thu thực nhận cho từng item
        Func<OrderDetailStatisticsItem, decimal> netRevenueSelector = od =>
            od.FinalDetailPrice ?? od.TotalPrice - (od.DetailDiscountPromotion ?? 0m);

        // Tùy theo range, chia nhỏ theo giờ/ngày/tháng/quý/năm
        switch (range)
        {
            case StatisticsTimeRange.Day:
                for (var hour = 0; hour < 24; hour++)
                {
                    categories.Add($"{hour}:00");
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                                                           od.CompletedAt.Value.Hour == hour);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(netRevenueSelector));
                }

                break;
            case StatisticsTimeRange.Week:
                var culture = CultureInfo.CurrentCulture;
                for (var i = 0; i < 7; i++)
                {
                    var dayName = culture.DateTimeFormat.GetDayName((DayOfWeek)i);
                    categories.Add(dayName);
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                                                           (int)od.CompletedAt.Value.DayOfWeek == i);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(netRevenueSelector));
                }

                break;
            case StatisticsTimeRange.Month:
                var daysInMonth = (end - start).Days;
                for (var day = 0; day < daysInMonth; day++)
                {
                    var date = start.AddDays(day);
                    categories.Add(date.ToString("dd/MM"));
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                                                           od.CompletedAt.Value.Date == date.Date);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(netRevenueSelector));
                }

                break;
            case StatisticsTimeRange.Quarter:
            case StatisticsTimeRange.Year:
                var months = range == StatisticsTimeRange.Quarter ? 3 : 12;
                for (var m = 0; m < months; m++)
                {
                    var monthDate = start.AddMonths(m);
                    categories.Add(monthDate.ToString("MM/yyyy"));
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                                                           od.CompletedAt.Value.Month == monthDate.Month &&
                                                           od.CompletedAt.Value.Year == monthDate.Year);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(netRevenueSelector));
                }

                break;
            case StatisticsTimeRange.Custom:
                var totalDays = (end - start).Days;
                for (var day = 0; day < totalDays; day++)
                {
                    var date = start.AddDays(day);
                    categories.Add(date.ToString("dd/MM"));
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                                                           od.CompletedAt.Value.Date == date.Date);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(netRevenueSelector));
                }

                break;
        }

        _loggerService.Info(
            $"[SellerStatistics] TimeSeries: {categories.Count} categories, {sales.Sum()} sales, {revenue.Sum()} revenue");

        return new SellerStatisticsResponseDto
        {
            Range = range.ToString(),
            Categories = categories,
            Sales = sales,
            Revenue = revenue
        };
    }

    /// <summary>
    /// Xác định khoảng thời gian thống kê dựa trên request.
    /// Đảm bảo DateTimeKind.Utc cho mọi giá trị.
    /// </summary>
    private (DateTime Start, DateTime End) GetStatisticsDateRange(SellerStatisticsRequestDto req)
    {
        var now = DateTime.UtcNow;
        DateTime start, end;
        switch (req.Range)
        {
            case StatisticsTimeRange.Day:
                start = now.Date;
                end = start.AddDays(1);
                break;
            case StatisticsTimeRange.Week:
                var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                start = now.AddDays(-1 * diff).Date;
                end = start.AddDays(7);
                break;
            case StatisticsTimeRange.Month:
                start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(1);
                break;
            case StatisticsTimeRange.Quarter:
                var quarter = (now.Month - 1) / 3 + 1;
                start = new DateTime(now.Year, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(3);
                break;
            case StatisticsTimeRange.Year:
                start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddYears(1);
                break;
            case StatisticsTimeRange.Custom:
                start = req.StartDate.HasValue
                    ? DateTime.SpecifyKind(req.StartDate.Value, DateTimeKind.Utc)
                    : now.Date;
                end = req.EndDate.HasValue
                    ? DateTime.SpecifyKind(req.EndDate.Value, DateTimeKind.Utc).AddDays(1)
                    : now.Date.AddDays(1);
                break;
            default:
                start = now.Date;
                end = start.AddDays(1);
                break;
        }

        _loggerService.Info($"[SellerStatistics] Calculated statistics range: {start:O} - {end:O}");
        return (start, end);
    }

    /// <summary>
    /// Xác định khoảng thời gian kỳ trước để tính growth.
    /// </summary>
    private (DateTime Start, DateTime End) GetPreviousDateRange(StatisticsTimeRange range, DateTime start, DateTime end)
    {
        switch (range)
        {
            case StatisticsTimeRange.Day:
                return (start.AddDays(-1), start);
            case StatisticsTimeRange.Week:
                return (start.AddDays(-7), start);
            case StatisticsTimeRange.Month:
                return (start.AddMonths(-1), start);
            case StatisticsTimeRange.Quarter:
                return (start.AddMonths(-3), start);
            case StatisticsTimeRange.Year:
                return (start.AddYears(-1), start);
            case StatisticsTimeRange.Custom:
                var period = end - start;
                return (start - period, start);
            default:
                return (start.AddDays(-1), start);
        }
    }

    /// <summary>
    /// Truy vấn order details cho seller trong khoảng thời gian, trả về danh sách DTO đã chuẩn hóa.
    /// </summary>
    private async Task<List<OrderDetailStatisticsItem>> GetOrderDetailsAsync(
        Guid sellerId,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        _loggerService.Info(
            $"[SellerStatistics] Querying OrderDetails for seller {sellerId} from {start:O} to {end:O}");
        var orderDetailsQuery = _unitOfWork.OrderDetails.GetQueryable()
            .AsNoTracking()
            .Where(od =>
                od.SellerId == sellerId &&
                od.Order.Status == OrderStatus.PAID.ToString() &&
                od.Status != OrderDetailItemStatus.CANCELLED &&
                od.Order.CompletedAt >= start &&
                od.Order.CompletedAt < end);

        var result = await orderDetailsQuery
            .Select(od => new OrderDetailStatisticsItem
            {
                OrderId = od.OrderId,
                Quantity = od.Quantity,
                TotalPrice = od.TotalPrice,
                DetailDiscountPromotion = od.DetailDiscountPromotion,
                FinalDetailPrice = od.FinalDetailPrice,
                ProductId = od.ProductId,
                ProductName = od.Product != null ? od.Product.Name : null,
                ProductImageUrl = od.Product != null ? od.Product.ImageUrls.FirstOrDefault() : null,
                ProductPrice = od.Product != null ? od.Product.Price : 0,
                BlindBoxId = od.BlindBoxId,
                BlindBoxName = od.BlindBox != null ? od.BlindBox.Name : null,
                BlindBoxImageUrl = od.BlindBox != null ? od.BlindBox.ImageUrl : null,
                BlindBoxPrice = od.BlindBox != null ? od.BlindBox.Price : 0,
                Status = od.Status.ToString(),
                CompletedAt = od.Order.CompletedAt
            })
            .ToListAsync(ct);

        _loggerService.Info($"[SellerStatistics] Loaded {result.Count} OrderDetails for statistics.");
        return result;
    }

    // API methods (unchanged signatures)
    public async Task<SellerOverviewStatisticsDto> GetOverviewStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return await BuildOverviewStatisticsAsync(sellerId, req, start, end, ct, orderDetails);
    }

    public async Task<List<TopSellingProductDto>> GetTopProductsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildTopProducts(orderDetails);
    }

    public async Task<List<TopSellingBlindBoxDto>> GetTopBlindBoxesAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildTopBlindBoxes(orderDetails);
    }

    public async Task<List<OrderStatusStatisticsDto>> GetOrderStatusStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildOrderStatusStatistics(orderDetails);
    }

    public async Task<SellerStatisticsResponseDto> GetTimeSeriesStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildTimeSeriesData(orderDetails, req.Range, start, end);
    }
}