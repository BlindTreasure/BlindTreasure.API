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

    public async Task<SellerDashboardStatisticsDto> GetDashboardStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        _loggerService.Info($"[SellerStatistics] Start dashboard statistics for seller {sellerId}");

        var (start, end) = GetStatisticsDateRange(req);
        _loggerService.Info($"[SellerStatistics] Date range: {start:O} - {end:O}");

        // Query all PAID orders for this seller in the time range
        var ordersQuery = _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.CompletedAt >= start
                        && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking();

        var orders = await ordersQuery.ToListAsync(ct);

        // Flatten all order details, filter out cancelled
        var orderDetails = orders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        // Build statistics
        var overview = await BuildOverviewStatisticsAsync(orders, orderDetails, req, start, end, ct);
        var topProducts = BuildTopProducts(orderDetails);
        var topBlindBoxes = BuildTopBlindBoxes(orderDetails);
        var orderStatusStats = BuildOrderStatusStatistics(orderDetails);
        var timeSeries = BuildTimeSeriesData(orders, orderDetails, req.Range, start, end);

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

    private async Task<SellerOverviewStatisticsDto> BuildOverviewStatisticsAsync(
        List<Order> orders,
        List<OrderDetail> orderDetails,
        SellerStatisticsRequestDto req,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        var totalOrders = orders.Count;
        var totalProductsSold = orderDetails.Sum(od => od.Quantity);

        var grossRevenue = orderDetails.Sum(od => od.TotalPrice);
        var totalDiscount = orderDetails.Sum(od => od.DetailDiscountPromotion ?? 0m);
        var netRevenue = orderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice - (od.DetailDiscountPromotion ?? 0m));

        // Refunds: Only from payments of these orders
        var orderIds = orders.Select(o => o.Id).ToList();
        var totalRefunded = await _unitOfWork.Payments.GetQueryable()
            .Where(p => orderIds.Contains(p.OrderId))
            .SumAsync(p => (decimal?)p.RefundedAmount) ?? 0m;

        netRevenue -= totalRefunded;

        var averageOrderValue = totalOrders > 0 ? Math.Round(netRevenue / totalOrders, 2) : 0m;

        // Previous period for growth calculation
        var (lastStart, lastEnd) = GetPreviousDateRange(req.Range, start, end);
        var lastOrders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == orders.FirstOrDefault().SellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.CompletedAt >= lastStart
                        && o.CompletedAt < lastEnd
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync(ct);

        var lastOrderDetails = lastOrders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        var lastOrdersCount = lastOrders.Count;
        var lastProductsSold = lastOrderDetails.Sum(od => od.Quantity);
        var lastRevenue = lastOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice - (od.DetailDiscountPromotion ?? 0m));
        var lastAOV = lastOrdersCount > 0 ? Math.Round(lastRevenue / lastOrdersCount, 2) : 0m;

        var revenueGrowth = lastRevenue > 0 ? Math.Round((netRevenue - lastRevenue) * 100 / lastRevenue, 2) : 0m;
        var ordersGrowth = lastOrdersCount > 0 ? Math.Round((decimal)(totalOrders - lastOrdersCount) * 100 / lastOrdersCount, 2) : 0m;
        var productsGrowth = lastProductsSold > 0
            ? Math.Round((decimal)(totalProductsSold - lastProductsSold) * 100 / lastProductsSold, 2)
            : 0m;
        var aovGrowth = lastAOV > 0 ? Math.Round((averageOrderValue - lastAOV) * 100 / lastAOV, 2) : 0m;

        return new SellerOverviewStatisticsDto
        {
            TotalRevenue = decimal.Round(netRevenue, 2),
            TotalRevenueLastPeriod = decimal.Round(lastRevenue, 2),
            RevenueGrowthPercent = revenueGrowth,
            TotalOrders = totalOrders,
            TotalOrdersLastPeriod = lastOrdersCount,
            OrdersGrowthPercent = ordersGrowth,
            TotalProductsSold = totalProductsSold,
            TotalProductsSoldLastPeriod = lastProductsSold,
            ProductsSoldGrowthPercent = productsGrowth,
            AverageOrderValue = averageOrderValue,
            AverageOrderValueLastPeriod = lastAOV,
            AverageOrderValueGrowthPercent = aovGrowth
        };
    }

    private List<TopSellingProductDto> BuildTopProducts(List<OrderDetail> orderDetails)
    {
        return orderDetails
            .Where(od => od.ProductId != null && od.Product != null)
            .GroupBy(od => od.ProductId)
            .Select(g => new TopSellingProductDto
            {
                ProductId = g.Key!.Value,
                ProductName = g.First().Product!.Name,
                ProductImageUrl = g.First().Product!.ImageUrls?.FirstOrDefault() ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice - (x.DetailDiscountPromotion ?? 0m)),
                Price = g.First().Product!.Price
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();
    }

    private List<TopSellingBlindBoxDto> BuildTopBlindBoxes(List<OrderDetail> orderDetails)
    {
        return orderDetails
            .Where(od => od.BlindBoxId != null && od.BlindBox != null)
            .GroupBy(od => od.BlindBoxId)
            .Select(g => new TopSellingBlindBoxDto
            {
                BlindBoxId = g.Key!.Value,
                BlindBoxName = g.First().BlindBox!.Name,
                BlindBoxImageUrl = g.First().BlindBox!.ImageUrl ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice - (x.DetailDiscountPromotion ?? 0m)),
                Price = g.First().BlindBox!.Price
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();
    }

    private List<OrderStatusStatisticsDto> BuildOrderStatusStatistics(List<OrderDetail> orderDetails)
    {
        var totalOrders = orderDetails.Select(od => od.OrderId).Distinct().Count();
        return orderDetails
            .GroupBy(od => od.Status.ToString())
            .Select(g => new OrderStatusStatisticsDto
            {
                Status = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice - (x.DetailDiscountPromotion ?? 0m)),
                Percentage = totalOrders > 0 ? Math.Round((decimal)g.Count() * 100 / totalOrders, 2) : 0m
            })
            .ToList();
    }

    private SellerStatisticsResponseDto BuildTimeSeriesData(
        List<Order> orders,
        List<OrderDetail> orderDetails,
        StatisticsTimeRange range,
        DateTime start,
        DateTime end)
    {
        var categories = new List<string>();
        var sales = new List<int>();
        var revenue = new List<decimal>();

        Func<OrderDetail, decimal> netRevenueSelector = od =>
            od.FinalDetailPrice ?? od.TotalPrice - (od.DetailDiscountPromotion ?? 0m);

        switch (range)
        {
            case StatisticsTimeRange.Day:
                for (var hour = 0; hour < 24; hour++)
                {
                    categories.Add($"{hour}:00");
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                                                           od.Order.CompletedAt.Value.Hour == hour);
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                                                           (int)od.Order.CompletedAt.Value.DayOfWeek == i);
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                                                           od.Order.CompletedAt.Value.Date == date.Date);
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                                                           od.Order.CompletedAt.Value.Month == monthDate.Month &&
                                                           od.Order.CompletedAt.Value.Year == monthDate.Year);
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                                                           od.Order.CompletedAt.Value.Date == date.Date);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(netRevenueSelector));
                }
                break;
        }

        return new SellerStatisticsResponseDto
        {
            Range = range.ToString(),
            Categories = categories,
            Sales = sales,
            Revenue = revenue
        };
    }

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
        return (start, end);
    }

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

    // API methods
    public async Task<SellerOverviewStatisticsDto> GetOverviewStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.CompletedAt >= start
                        && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync(ct);

        var orderDetails = orders.SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        return await BuildOverviewStatisticsAsync(orders, orderDetails, req, start, end, ct);
    }

    public async Task<List<TopSellingProductDto>> GetTopProductsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.CompletedAt >= start
                        && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
            .AsNoTracking()
            .ToListAsync(ct);

        var orderDetails = orders.SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        return BuildTopProducts(orderDetails);
    }

    public async Task<List<TopSellingBlindBoxDto>> GetTopBlindBoxesAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.CompletedAt >= start
                        && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.BlindBox)
            .AsNoTracking()
            .ToListAsync(ct);

        var orderDetails = orders.SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        return BuildTopBlindBoxes(orderDetails);
    }

    public async Task<List<OrderStatusStatisticsDto>> GetOrderStatusStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.CompletedAt >= start
                        && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync(ct);

        var orderDetails = orders.SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        return BuildOrderStatusStatistics(orderDetails);
    }

    public async Task<SellerStatisticsResponseDto> GetTimeSeriesStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.CompletedAt >= start
                        && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync(ct);

        var orderDetails = orders.SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        return BuildTimeSeriesData(orders, orderDetails, req.Range, start, end);
    }
}