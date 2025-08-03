using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services;

public class SellerStatisticsService : ISellerStatisticsService
{
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapper;
    private readonly INotificationService _notificationService;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;

    public SellerStatisticsService(
        IBlobService blobService,
        IEmailService emailService,
        ILoggerService loggerService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IMapperService mapper,
        IClaimsService claimsService,
        IProductService productService,
        INotificationService notificationService)
    {
        _blobService = blobService;
        _emailService = emailService;
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _mapper = mapper;
        _claimsService = claimsService;
        _productService = productService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Lấy thống kê tổng quan cho seller dashboard
    /// </summary>
    public async Task<SellerDashboardStatisticsDto> GetDashboardStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        var (start, end) = GetStatisticsDateRange(req);

        var orderDetailsQuery = _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Include(od => od.Product)
            .Include(od => od.BlindBox)
            .Where(od =>
                od.SellerId == sellerId &&
                od.Order.Status == OrderStatus.PAID.ToString() &&
                od.Status != OrderDetailItemStatus.CANCELLED &&
                od.Order.CompletedAt >= start &&
                od.Order.CompletedAt < end);

        var orderDetails = await orderDetailsQuery.ToListAsync(ct);

        var orderIds = orderDetails.Select(od => od.OrderId).Distinct().ToList();
        var totalOrders = orderIds.Count;
        var totalProductsSold = orderDetails.Sum(od => od.Quantity);
        var grossRevenue = orderDetails.Sum(od => od.TotalPrice);

        var totalDiscount = await _unitOfWork.OrderSellerPromotions.GetQueryable()
            .Where(osp => osp.SellerId == sellerId && orderIds.Contains(osp.OrderId))
            .SumAsync(osp => osp.DiscountAmount);

        var payments = await _unitOfWork.Payments.GetQueryable()
            .Where(p => orderIds.Contains(p.OrderId))
            .ToListAsync(ct);
        var totalRefunded = payments.Sum(p => p.RefundedAmount);

        var totalShippingFee = orderDetails.Sum(od => od.Shipments?.Sum(s => s.TotalFee) ?? 0);

        var netRevenue = grossRevenue - totalDiscount - totalRefunded;
        var averageOrderValue = totalOrders > 0 ? Math.Round(netRevenue / totalOrders, 2) : 0m;
        var refundRate = grossRevenue > 0 ? Math.Round(totalRefunded / grossRevenue, 4) : 0m;

        var timeSeries = BuildTimeSeriesData(orderDetails, req.Range, start, end);

        var topProducts = orderDetails
            .Where(od => od.ProductId.HasValue && od.Product != null)
            .GroupBy(od => od.ProductId)
            .Select(g => new TopSellingProductDto
            {
                ProductId = g.Key!.Value,
                ProductName = g.First().Product.Name,
                ProductImageUrl = g.First().Product.ImageUrls?.FirstOrDefault() ?? "",
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalPrice),
                Price = g.First().Product.Price
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();

        var topBlindBoxes = orderDetails
            .Where(od => od.BlindBoxId.HasValue && od.BlindBox != null)
            .GroupBy(od => od.BlindBoxId)
            .Select(g => new TopSellingBlindBoxDto
            {
                BlindBoxId = g.Key!.Value,
                BlindBoxName = g.First().BlindBox.Name,
                BlindBoxImageUrl = g.First().BlindBox.ImageUrl ?? "",
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalPrice),
                Price = g.First().BlindBox.Price
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToList();

        var orderStatusStats = orderDetails
            .GroupBy(od => od.Status.ToString())
            .Select(g => new OrderStatusStatisticsDto
            {
                Status = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(x => x.TotalPrice),
                Percentage = totalOrders > 0 ? Math.Round((decimal)g.Count() * 100 / totalOrders, 2) : 0m
            })
            .ToList();

        var overview = await BuildOverviewStatisticsAsync(sellerId, req, start, end, ct, grossRevenue, totalOrders, totalProductsSold, averageOrderValue);

        return new SellerDashboardStatisticsDto
        {
            Overview = overview,
            TopProducts = topProducts,
            TopBlindBoxes = topBlindBoxes,
            OrderStatusStats = orderStatusStats,
            LastUpdated = DateTime.UtcNow
        };
    }

    private SellerStatisticsResponseDto BuildTimeSeriesData(
        List<OrderDetail> orderDetails,
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                        od.Order.CompletedAt.Value.Hour == hour);
                    sales.Add(details.Count());
                    revenue.Add(details.Sum(od => od.TotalPrice));
                }
                break;
            case StatisticsTimeRange.Week:
                var daysOfWeek = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                for (int i = 0; i < 7; i++)
                {
                    categories.Add(daysOfWeek[i]);
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                        (int)od.Order.CompletedAt.Value.DayOfWeek == (i == 6 ? 0 : i + 1));
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                        od.Order.CompletedAt.Value.Date == date.Date);
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                        od.Order.CompletedAt.Value.Month == monthDate.Month &&
                        od.Order.CompletedAt.Value.Year == monthDate.Year);
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
                    var details = orderDetails.Where(od => od.Order.CompletedAt.HasValue &&
                        od.Order.CompletedAt.Value.Date == date.Date);
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
                start = new DateTime(now.Year, now.Month, 1);
                end = start.AddMonths(1);
                break;
            case StatisticsTimeRange.Quarter:
                int quarter = (now.Month - 1) / 3 + 1;
                start = new DateTime(now.Year, (quarter - 1) * 3 + 1, 1);
                end = start.AddMonths(3);
                break;
            case StatisticsTimeRange.Year:
                start = new DateTime(now.Year, 1, 1);
                end = start.AddYears(1);
                break;
            case StatisticsTimeRange.Custom:
                start = req.StartDate ?? now.Date;
                end = req.EndDate?.AddDays(1) ?? now.Date.AddDays(1);
                break;
            default:
                start = now.Date;
                end = start.AddDays(1);
                break;
        }
        return (start, end);
    }

    private async Task<SellerOverviewStatisticsDto> BuildOverviewStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        DateTime start,
        DateTime end,
        CancellationToken ct,
        decimal currentRevenue,
        int currentOrders,
        int currentProductsSold,
        decimal currentAOV)
    {
        DateTime lastStart, lastEnd;
        switch (req.Range)
        {
            case StatisticsTimeRange.Day:
                lastStart = start.AddDays(-1);
                lastEnd = start;
                break;
            case StatisticsTimeRange.Week:
                lastStart = start.AddDays(-7);
                lastEnd = start;
                break;
            case StatisticsTimeRange.Month:
                lastStart = start.AddMonths(-1);
                lastEnd = start;
                break;
            case StatisticsTimeRange.Quarter:
                lastStart = start.AddMonths(-3);
                lastEnd = start;
                break;
            case StatisticsTimeRange.Year:
                lastStart = start.AddYears(-1);
                lastEnd = start;
                break;
            case StatisticsTimeRange.Custom:
                var period = end - start;
                lastStart = start - period;
                lastEnd = start;
                break;
            default:
                lastStart = start.AddDays(-1);
                lastEnd = start;
                break;
        }

        var lastOrderDetails = await _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Where(od =>
                od.SellerId == sellerId &&
                od.Order.Status == OrderStatus.PAID.ToString() &&
                od.Status != OrderDetailItemStatus.CANCELLED &&
                od.Order.CompletedAt >= lastStart &&
                od.Order.CompletedAt < lastEnd)
            .ToListAsync(ct);

        var lastOrderIds = lastOrderDetails.Select(od => od.OrderId).Distinct().ToList();
        var lastRevenue = lastOrderDetails.Sum(od => od.TotalPrice);
        var lastOrders = lastOrderIds.Count;
        var lastProductsSold = lastOrderDetails.Sum(od => od.Quantity);
        var lastAOV = lastOrders > 0 ? Math.Round(lastRevenue / lastOrders, 2) : 0m;

        decimal revenueGrowth = lastRevenue > 0 ? Math.Round((currentRevenue - lastRevenue) * 100 / lastRevenue, 2) : 0m;
        decimal ordersGrowth = lastOrders > 0 ? Math.Round( (decimal)(currentOrders - lastOrders) * 100 / lastOrders, 2) : 0m;
        decimal productsGrowth = lastProductsSold > 0 ? Math.Round((decimal)(currentProductsSold - lastProductsSold) * 100 / lastProductsSold, 2) : 0m;
        decimal aovGrowth = lastAOV > 0 ? Math.Round((currentAOV - lastAOV) * 100 / lastAOV, 2) : 0m;

        var timeSeries = BuildTimeSeriesData(
            await _unitOfWork.OrderDetails.GetQueryable()
                .Include(od => od.Order)
                .Where(od =>
                    od.SellerId == sellerId &&
                    od.Order.Status == OrderStatus.PAID.ToString() &&
                    od.Status != OrderDetailItemStatus.CANCELLED &&
                    od.Order.CompletedAt >= start &&
                    od.Order.CompletedAt < end)
                .ToListAsync(ct),
            req.Range, start, end);

        return new SellerOverviewStatisticsDto
        {
            TotalRevenue = decimal.Round(currentRevenue, 2),
            TotalRevenueLastPeriod = decimal.Round(lastRevenue, 2),
            RevenueGrowthPercent = revenueGrowth,

            TotalOrders = currentOrders,
            TotalOrdersLastPeriod = lastOrders,
            OrdersGrowthPercent = ordersGrowth,

            TotalProductsSold = currentProductsSold,
            TotalProductsSoldLastPeriod = lastProductsSold,
            ProductsSoldGrowthPercent = productsGrowth,

            AverageOrderValue = currentAOV,
            AverageOrderValueLastPeriod = lastAOV,
            AverageOrderValueGrowthPercent = aovGrowth,

            TimeSeriesData = timeSeries
        };
    }
}
