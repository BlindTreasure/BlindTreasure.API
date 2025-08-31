using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

/// <summary>
///     DTO dùng cho thống kê order detail, thay thế anonymous type.
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
    public DateTime? PlacedAt { get; set; } // NEW: Add PlacedAt for PAID orders
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

        // FIX: Use COMPLETED orders for dashboard statistics, not PAID
        var orders = await GetOrdersInRangeAsync(sellerId, start, end, ct);
        var orderDetails = orders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        // Build statistics
        var overview = await BuildOverviewStatisticsAsync(orders, orderDetails, req, start, end, ct, sellerId);
        var topProducts = BuildTopProducts(orderDetails);
        var topBlindBoxes = BuildTopBlindBoxes(orderDetails);
        var orderStatusStats = BuildOrderStatusStatistics(orderDetails);
        var timeSeries = await BuildTimeSeriesData(sellerId, req.Range, start, end);

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

    // API methods - FIX: All use centralized GetOrdersInRangeAsync
    public async Task<SellerOverviewStatisticsDto> GetOverviewStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orders = await GetOrdersInRangeAsync(sellerId, start, end, ct);
        var orderDetails = orders.SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        return await BuildOverviewStatisticsAsync(orders, orderDetails, req, start, end, ct, sellerId);
    }

    public async Task<List<TopSellingProductDto>> GetTopProductsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orders = await GetOrdersInRangeAsync(sellerId, start, end, ct);
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
        var orders = await GetOrdersInRangeAsync(sellerId, start, end, ct);
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
        var orders = await GetOrdersInRangeAsync(sellerId, start, end, ct);
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
        var orders = await GetOrdersInRangeAsync(sellerId, start, end, ct);
        var orderDetails = orders.SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();
        var result = await BuildTimeSeriesData(sellerId, req.Range, start, end);
        return result;
    }

    public async Task<SellerRevenueSummaryDto> GetRevenueSummaryAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);

        // Estimated: PAID orders (using PlacedAt)
        var paidOrders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.PlacedAt >= start && o.PlacedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync(ct);

        var estimatedRevenue = paidOrders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .Sum(od => od.FinalDetailPrice ?? od.TotalPrice);

        // Actual: COMPLETED orders (using CompletedAt)
        var completedOrders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.COMPLETED.ToString()
                        && o.CompletedAt >= start && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync(ct);

        var actualRevenue = completedOrders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .Sum(od => od.FinalDetailPrice ?? od.TotalPrice);

        return new SellerRevenueSummaryDto
        {
            EstimatedRevenue = decimal.Round(estimatedRevenue, 2),
            ActualRevenue = decimal.Round(actualRevenue, 2),
            EstimatedOrderCount = paidOrders.Count,
            ActualOrderCount = completedOrders.Count,
            PeriodStart = start,
            PeriodEnd = end
        };
    }

    /// <summary>
    ///     Centralized method to get orders in date range - FIX: Use COMPLETED orders and CompletedAt
    /// </summary>
    private async Task<List<Order>> GetOrdersInRangeAsync(
        Guid sellerId,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        return await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.COMPLETED.ToString() // FIX: Use COMPLETED
                        && o.CompletedAt >= start && o.CompletedAt < end // FIX: Use CompletedAt consistently
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.BlindBox)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private async Task<SellerOverviewStatisticsDto> BuildOverviewStatisticsAsync(
        List<Order> completedOrders,
        List<OrderDetail> completedOrderDetails,
        SellerStatisticsRequestDto req,
        DateTime start,
        DateTime end,
        CancellationToken ct,
        Guid sellerId)
    {
        // Get PAID orders for EstimatedRevenue (filter by PlacedAt)
        var paidOrders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.PlacedAt >= start && o.PlacedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync(ct);

        var estimatedRevenue = paidOrders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .Sum(od => od.FinalDetailPrice ?? od.TotalPrice);

        // ActualRevenue: from completedOrders (already filtered by CompletedAt)
        var actualRevenue = completedOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice);

        var totalOrders = completedOrders.Count;
        var totalProductsSold = completedOrderDetails.Sum(od => od.Quantity);

        // Refunds for these COMPLETED orders
        var orderIds = completedOrders.Select(o => o.Id).ToHashSet();
        var totalRefunded = await _unitOfWork.Payments.GetQueryable()
            .Where(p => orderIds.Contains(p.OrderId) && p.RefundedAmount > 0)
            .SumAsync(p => (decimal?)p.RefundedAmount, ct) ?? 0m;

        var finalRevenue = actualRevenue - totalRefunded;
        var averageOrderValue = totalOrders > 0 ? Math.Round(finalRevenue / totalOrders, 2) : 0m;

        // Previous period for growth calculation (use CompletedAt for previous period)
        var (lastStart, lastEnd) = GetPreviousDateRange(req.Range, start, end);
        var lastOrders = await GetOrdersInRangeAsync(sellerId, lastStart, lastEnd, ct);
        var lastOrderDetails = lastOrders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
            .ToList();

        var lastActualRevenue = lastOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice);
        var lastOrderIds = lastOrders.Select(o => o.Id).ToHashSet();
        var lastRefunded = await _unitOfWork.Payments.GetQueryable()
            .Where(p => lastOrderIds.Contains(p.OrderId) && p.RefundedAmount > 0)
            .SumAsync(p => (decimal?)p.RefundedAmount, ct) ?? 0m;

        var lastFinalRevenue = lastActualRevenue - lastRefunded;
        var lastAOV = lastOrders.Count > 0 ? Math.Round(lastFinalRevenue / lastOrders.Count, 2) : 0m;

        // Growth calculations
        var estimatedRevenueGrowth = lastFinalRevenue != 0
            ? Math.Round((finalRevenue - lastFinalRevenue) * 100 / Math.Abs(lastFinalRevenue), 2)
            : finalRevenue > 0
                ? 100m
                : 0m;

        var ordersGrowth = lastOrders.Count != 0
            ? Math.Round((decimal)(totalOrders - lastOrders.Count) * 100 / lastOrders.Count, 2)
            : totalOrders > 0
                ? 100m
                : 0m;

        var productsGrowth = lastOrderDetails.Sum(od => od.Quantity) != 0
            ? Math.Round(
                (decimal)(totalProductsSold - lastOrderDetails.Sum(od => od.Quantity)) * 100 /
                lastOrderDetails.Sum(od => od.Quantity), 2)
            : totalProductsSold > 0
                ? 100m
                : 0m;

        var aovGrowth = lastAOV != 0 ? Math.Round((averageOrderValue - lastAOV) * 100 / Math.Abs(lastAOV), 2) :
            averageOrderValue > 0 ? 100m : 0m;

        return new SellerOverviewStatisticsDto
        {
            EstimatedRevenue = decimal.Round(estimatedRevenue, 2),
            EstimatedRevenueLastPeriod = decimal.Round(lastFinalRevenue, 2),
            EstimatedRevenueGrowthPercent = estimatedRevenueGrowth,

            ActualRevenue = decimal.Round(finalRevenue, 2),
            ActualRevenueLastPeriod = decimal.Round(lastFinalRevenue, 2),
            ActualRevenueGrowthPercent = estimatedRevenueGrowth,

            TotalOrders = totalOrders,
            TotalOrdersLastPeriod = lastOrders.Count,
            OrdersGrowthPercent = ordersGrowth,
            TotalProductsSold = totalProductsSold,
            TotalProductsSoldLastPeriod = lastOrderDetails.Sum(od => od.Quantity),
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
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice), // FIX: Simplified calculation
                Price = g.First().Product!.RealSellingPrice
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
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice), // FIX: Simplified calculation
                Price = g.First().BlindBox!.Price
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();
    }

    private List<OrderStatusStatisticsDto> BuildOrderStatusStatistics(List<OrderDetail> orderDetails)
    {
        var statusGroups = orderDetails
            .GroupBy(od => od.Status.ToString())
            .ToList();

        var totalCount = statusGroups.Sum(g => g.Count());

        return statusGroups
            .Select(g => new OrderStatusStatisticsDto
            {
                Status = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(x => x.FinalDetailPrice ?? x.TotalPrice), // FIX: Simplified calculation
                Percentage = totalCount > 0 ? Math.Round((decimal)g.Count() * 100 / totalCount, 2) : 0m
            })
            .ToList();
    }

    private async Task<SellerStatisticsResponseDto> BuildTimeSeriesData(
        Guid sellerId,
        StatisticsTimeRange range,
        DateTime start,
        DateTime end
    )
    {
        // Get PAID orders for EstimatedRevenue (filter by PlacedAt)
        var paidOrders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.PAID.ToString()
                        && o.PlacedAt >= start && o.PlacedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync();

        // Get COMPLETED orders for ActualRevenue (filter by CompletedAt)
        var completedOrders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == sellerId
                        && o.Status == OrderStatus.COMPLETED.ToString()
                        && o.CompletedAt >= start && o.CompletedAt < end
                        && !o.IsDeleted)
            .Include(o => o.OrderDetails)
            .AsNoTracking()
            .ToListAsync();

        var categories = new List<string>();
        var sales = new List<int>();
        var actualRevenue = new List<decimal>();
        var estimatedRevenue = new List<decimal>();

        Func<OrderDetail, decimal> revenueSelector = od => od.FinalDetailPrice ?? od.TotalPrice;

        var totalDays = (end - start).Days;
        for (var day = 0; day < totalDays; day++)
        {
            var date = start.AddDays(day);
            categories.Add(date.ToString("dd/MM"));

            // ActualRevenue: COMPLETED orders for this day (by CompletedAt)
            var completedDetails = completedOrders
                .Where(o => o.CompletedAt.HasValue && o.CompletedAt.Value.Date == date.Date)
                .SelectMany(o => o.OrderDetails)
                .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
                .ToList();

            actualRevenue.Add(completedDetails.Sum(revenueSelector));
            sales.Add(completedDetails.Sum(od => od.Quantity));

            // EstimatedRevenue: PAID orders for this day (by PlacedAt)
            var paidDetails = paidOrders
                .Where(o => o.PlacedAt.Date == date.Date)
                .SelectMany(o => o.OrderDetails)
                .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
                .ToList();

            estimatedRevenue.Add(paidDetails.Sum(revenueSelector));
        }

        return new SellerStatisticsResponseDto
        {
            Range = range.ToString(),
            Categories = categories,
            Sales = sales,
            ActualRevenue = actualRevenue,
            EstimatedRevenue = estimatedRevenue
        };
    }

    private (DateTime Start, DateTime End) GetStatisticsDateRange(SellerStatisticsRequestDto req)
    {
        var now = DateTime.UtcNow;
        DateTime start, end;

        switch (req.Range)
        {
            case StatisticsTimeRange.Day:
                var dayBase = req.StartDate?.Date ?? now.Date;
                start = DateTime.SpecifyKind(dayBase, DateTimeKind.Utc);
                end = start.AddDays(1);
                break;

            case StatisticsTimeRange.Week:
                // Use StartDate if provided, otherwise use current date
                var weekBase = req.StartDate?.Date ?? now.Date;
                // Always start from Monday (ISO standard)
                var diff = (7 + (int)weekBase.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                start = DateTime.SpecifyKind(weekBase.AddDays(-diff), DateTimeKind.Utc);
                end = start.AddDays(7);
                break;

            case StatisticsTimeRange.Month:
                var monthBase = req.StartDate?.Date ?? now.Date;
                start = new DateTime(monthBase.Year, monthBase.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(1);
                break;

            case StatisticsTimeRange.Quarter:
                var quarterBase = req.StartDate?.Date ?? now.Date;
                var quarter = (quarterBase.Month - 1) / 3 + 1;
                start = new DateTime(quarterBase.Year, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(3);
                break;

            case StatisticsTimeRange.Year:
                var yearBase = req.StartDate?.Date ?? now.Date;
                start = new DateTime(yearBase.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddYears(1);
                break;

            case StatisticsTimeRange.Custom:
                start = req.StartDate.HasValue
                    ? DateTime.SpecifyKind(req.StartDate.Value.Date, DateTimeKind.Utc)
                    : DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                end = req.EndDate.HasValue
                    ? DateTime.SpecifyKind(req.EndDate.Value.Date, DateTimeKind.Utc).AddDays(1)
                    : start.AddDays(1);
                if (end <= start)
                    end = start.AddDays(1);
                break;

            default:
                start = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                end = start.AddDays(1);
                break;
        }

        return (start, end);
    }

    private (DateTime Start, DateTime End) GetPreviousDateRange(StatisticsTimeRange range, DateTime start, DateTime end)
    {
        var period = end - start;
        return range switch
        {
            StatisticsTimeRange.Day => (start.AddDays(-1), start),
            StatisticsTimeRange.Week => (start.AddDays(-7), start),
            StatisticsTimeRange.Month => (start.AddMonths(-1), start),
            StatisticsTimeRange.Quarter => (start.AddMonths(-3), start),
            StatisticsTimeRange.Year => (start.AddYears(-1), start),
            StatisticsTimeRange.Custom => (start - period, start),
            _ => (start.AddDays(-1), start)
        };
    }
}

public class SellerRevenueSummaryDto
{
    public decimal EstimatedRevenue { get; set; } // Doanh thu ước tính (PAID)
    public decimal ActualRevenue { get; set; } // Doanh thu thật (COMPLETED)
    public int EstimatedOrderCount { get; set; }
    public int ActualOrderCount { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}