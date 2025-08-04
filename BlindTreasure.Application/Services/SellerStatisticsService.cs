using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services;

/// <summary>
/// DTO dùng cho thống kê order detail, thay thế anonymous type.
/// </summary>
public class OrderDetailStatisticsItem
{
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
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

    public SellerStatisticsService(
        ILoggerService loggerService,
        IUnitOfWork unitOfWork)
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

        var (start, end) = GetStatisticsDateRange(req);
        _loggerService.Info($"[SellerStatistics] Date range: {start:O} - {end:O}");

        // Truy vấn orderDetails đã filter, chỉ lấy các trường cần thiết
        var orderDetailsQuery = _unitOfWork.OrderDetails.GetQueryable()
            .AsNoTracking()
            .Where(od =>
                od.SellerId == sellerId &&
                od.Order.Status == OrderStatus.PAID.ToString() &&
                od.Status != OrderDetailItemStatus.CANCELLED &&
                od.Order.CompletedAt >= start &&
                od.Order.CompletedAt < end);

        // Sử dụng DTO thay cho anonymous type
        var orderDetails = await orderDetailsQuery
            .Select(od => new OrderDetailStatisticsItem
            {
                OrderId = od.OrderId,
                Quantity = od.Quantity,
                TotalPrice = od.TotalPrice,
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

        // Lấy danh sách orderId
        var orderIds = orderDetails.Select(od => od.OrderId).Distinct().ToList();

        // Tính tổng discount trực tiếp trên DB
        var totalDiscount = await _unitOfWork.OrderSellerPromotions.GetQueryable()
            .Where(osp => osp.SellerId == sellerId && orderIds.Contains(osp.OrderId))
            .SumAsync(osp => (decimal?)osp.DiscountAmount) ?? 0m;

        // Tính tổng refund trực tiếp trên DB
        var totalRefunded = await _unitOfWork.Payments.GetQueryable()
            .Where(p => orderIds.Contains(p.OrderId))
            .SumAsync(p => (decimal?)p.RefundedAmount) ?? 0m;

        // Build các module thống kê
        var overview = await BuildOverviewStatisticsAsync(sellerId, req, start, end, ct, orderDetails, totalDiscount, totalRefunded);
        var topProducts = BuildTopProducts(orderDetails);
        var topBlindBoxes = BuildTopBlindBoxes(orderDetails);
        var orderStatusStats = BuildOrderStatusStatistics(orderDetails);
        var timeSeries = BuildTimeSeriesData(orderDetails, req.Range, start, end);

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
        List<OrderDetailStatisticsItem> orderDetails,
        decimal totalDiscount,
        decimal totalRefunded)
    {
        // Tổng hợp các chỉ số hiện tại
        var orderIds = orderDetails.Select(od => od.OrderId).Distinct().ToList();
        var totalOrders = orderIds.Count;
        var totalProductsSold = orderDetails.Sum(od => od.Quantity);
        var grossRevenue = orderDetails.Sum(od => od.TotalPrice);
        var netRevenue = grossRevenue - totalDiscount - totalRefunded;
        var averageOrderValue = totalOrders > 0 ? Math.Round(netRevenue / totalOrders, 2) : 0m;

        // Lấy dữ liệu kỳ trước để tính growth
        var (lastStart, lastEnd) = GetPreviousDateRange(req.Range, start, end);
        var lastOrderDetailsQuery = _unitOfWork.OrderDetails.GetQueryable()
            .AsNoTracking()
            .Where(od =>
                od.SellerId == sellerId &&
                od.Order.Status == OrderStatus.PAID.ToString() &&
                od.Status != OrderDetailItemStatus.CANCELLED &&
                od.Order.CompletedAt >= lastStart &&
                od.Order.CompletedAt < lastEnd);

        var lastOrderDetails = await lastOrderDetailsQuery
            .Select(od => new OrderDetailStatisticsItem
            {
                OrderId = od.OrderId,
                Quantity = od.Quantity,
                TotalPrice = od.TotalPrice
            })
            .ToListAsync(ct);

        var lastOrderIds = lastOrderDetails.Select(od => od.OrderId).Distinct().ToList();
        var lastOrders = lastOrderIds.Count;
        var lastProductsSold = lastOrderDetails.Sum(od => od.Quantity);
        var lastRevenue = lastOrderDetails.Sum(od => od.TotalPrice);
        var lastAOV = lastOrders > 0 ? Math.Round(lastRevenue / lastOrders, 2) : 0m;

        // Tính growth
        decimal revenueGrowth = lastRevenue > 0 ? Math.Round((netRevenue - lastRevenue) * 100 / lastRevenue, 2) : 0m;
        decimal ordersGrowth = lastOrders > 0 ? Math.Round((decimal)(totalOrders - lastOrders) * 100 / lastOrders, 2) : 0m;
        decimal productsGrowth = lastProductsSold > 0 ? Math.Round((decimal)(totalProductsSold - lastProductsSold) * 100 / lastProductsSold, 2) : 0m;
        decimal aovGrowth = lastAOV > 0 ? Math.Round((averageOrderValue - lastAOV) * 100 / lastAOV, 2) : 0m;

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
            AverageOrderValueGrowthPercent = aovGrowth,

            TimeSeriesData = null // Gán ở orchestration nếu cần
        };
    }

    /// <summary>
    /// Trả về top 5 sản phẩm bán chạy nhất.
    /// </summary>
    private List<TopSellingProductDto> BuildTopProducts(List<OrderDetailStatisticsItem> orderDetails)
    {
        return orderDetails
            .Where(od => od.ProductId != null)
            .GroupBy(od => od.ProductId)
            .Select(g => new TopSellingProductDto
            {
                ProductId = g.Key!.Value,
                ProductName = g.First().ProductName ?? string.Empty,
                ProductImageUrl = g.First().ProductImageUrl ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalPrice),
                Price = g.First().ProductPrice
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Trả về top 5 blindbox bán chạy nhất.
    /// </summary>
    private List<TopSellingBlindBoxDto> BuildTopBlindBoxes(List<OrderDetailStatisticsItem> orderDetails)
    {
        return orderDetails
            .Where(od => od.BlindBoxId != null)
            .GroupBy(od => od.BlindBoxId)
            .Select(g => new TopSellingBlindBoxDto
            {
                BlindBoxId = g.Key!.Value,
                BlindBoxName = g.First().BlindBoxName ?? string.Empty,
                BlindBoxImageUrl = g.First().BlindBoxImageUrl ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalPrice),
                Price = g.First().BlindBoxPrice
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Thống kê trạng thái đơn hàng.
    /// </summary>
    private List<OrderStatusStatisticsDto> BuildOrderStatusStatistics(List<OrderDetailStatisticsItem> orderDetails)
    {
        var totalOrders = orderDetails.Select(od => od.OrderId).Distinct().Count();
        return orderDetails
            .GroupBy(od => od.Status)
            .Select(g => new OrderStatusStatisticsDto
            {
                Status = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(x => x.TotalPrice),
                Percentage = totalOrders > 0 ? Math.Round((decimal)g.Count() * 100 / totalOrders, 2) : 0m
            })
            .ToList();
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

        switch (range)
        {
            case StatisticsTimeRange.Day:
                for (int hour = 0; hour < 24; hour++)
                {
                    categories.Add($"{hour}:00");
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                        od.CompletedAt.Value.Hour == hour);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(od => od.TotalPrice));
                }
                break;
            case StatisticsTimeRange.Week:
                var culture = CultureInfo.CurrentCulture;
                for (int i = 0; i < 7; i++)
                {
                    var dayName = culture.DateTimeFormat.GetDayName((DayOfWeek)i);
                    categories.Add(dayName);
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                        (int)od.CompletedAt.Value.DayOfWeek == i);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(od => od.TotalPrice));
                }
                break;
            case StatisticsTimeRange.Month:
                int daysInMonth = (end - start).Days;
                for (int day = 0; day < daysInMonth; day++)
                {
                    var date = start.AddDays(day);
                    categories.Add(date.ToString("dd/MM"));
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                        od.CompletedAt.Value.Date == date.Date);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(od => od.TotalPrice));
                }
                break;
            case StatisticsTimeRange.Quarter:
            case StatisticsTimeRange.Year:
                int months = range == StatisticsTimeRange.Quarter ? 3 : 12;
                for (int m = 0; m < months; m++)
                {
                    var monthDate = start.AddMonths(m);
                    categories.Add(monthDate.ToString("MM/yyyy"));
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                        od.CompletedAt.Value.Month == monthDate.Month &&
                        od.CompletedAt.Value.Year == monthDate.Year);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(od => od.TotalPrice));
                }
                break;
            case StatisticsTimeRange.Custom:
                int totalDays = (end - start).Days;
                for (int day = 0; day < totalDays; day++)
                {
                    var date = start.AddDays(day);
                    categories.Add(date.ToString("dd/MM"));
                    var details = orderDetails.Where(od => od.CompletedAt.HasValue &&
                        od.CompletedAt.Value.Date == date.Date);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(od => od.TotalPrice));
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
                int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                start = now.AddDays(-1 * diff).Date;
                end = start.AddDays(7);
                break;
            case StatisticsTimeRange.Month:
                start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(1);
                break;
            case StatisticsTimeRange.Quarter:
                int quarter = (now.Month - 1) / 3 + 1;
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
    /// API lấy thống kê tổng quan cho seller (Overview).
    /// </summary>
    public async Task<SellerOverviewStatisticsDto> GetOverviewStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);

        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);

        var orderIds = orderDetails.Select(od => od.OrderId).Distinct().ToList();

        var totalDiscount = await _unitOfWork.OrderSellerPromotions.GetQueryable()
            .Where(osp => osp.SellerId == sellerId && orderIds.Contains(osp.OrderId))
            .SumAsync(osp => (decimal?)osp.DiscountAmount) ?? 0m;

        var totalRefunded = await _unitOfWork.Payments.GetQueryable()
            .Where(p => orderIds.Contains(p.OrderId))
            .SumAsync(p => (decimal?)p.RefundedAmount) ?? 0m;

        return await BuildOverviewStatisticsAsync(sellerId, req, start, end, ct, orderDetails, totalDiscount, totalRefunded);
    }

    /// <summary>
    /// API lấy top 5 sản phẩm bán chạy nhất.
    /// </summary>
    public async Task<List<TopSellingProductDto>> GetTopProductsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildTopProducts(orderDetails);
    }

    /// <summary>
    /// API lấy top 5 blindbox bán chạy nhất.
    /// </summary>
    public async Task<List<TopSellingBlindBoxDto>> GetTopBlindBoxesAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildTopBlindBoxes(orderDetails);
    }

    /// <summary>
    /// API lấy thống kê trạng thái đơn hàng.
    /// </summary>
    public async Task<List<OrderStatusStatisticsDto>> GetOrderStatusStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildOrderStatusStatistics(orderDetails);
    }

    /// <summary>
    /// API lấy thống kê theo thời gian (time series).
    /// </summary>
    public async Task<SellerStatisticsResponseDto> GetTimeSeriesStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);
        var orderDetails = await GetOrderDetailsAsync(sellerId, start, end, ct);
        return BuildTimeSeriesData(orderDetails, req.Range, start, end);
    }

    // Helper method để tránh lặp lại truy vấn
    private async Task<List<OrderDetailStatisticsItem>> GetOrderDetailsAsync(
        Guid sellerId,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        var orderDetailsQuery = _unitOfWork.OrderDetails.GetQueryable()
            .AsNoTracking()
            .Where(od =>
                od.SellerId == sellerId &&
                od.Order.Status == OrderStatus.PAID.ToString() &&
                od.Status != OrderDetailItemStatus.CANCELLED &&
                od.Order.CompletedAt >= start &&
                od.Order.CompletedAt < end);

        return await orderDetailsQuery
            .Select(od => new OrderDetailStatisticsItem
            {
                OrderId = od.OrderId,
                Quantity = od.Quantity,
                TotalPrice = od.TotalPrice,
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
    }
}
