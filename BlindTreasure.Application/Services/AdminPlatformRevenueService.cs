using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.AdminStatisticDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services;

public class AdminPlatformRevenueService
{
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;

    public AdminPlatformRevenueService(
        ILoggerService logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    #region Helper Methods

    private (DateTime Start, DateTime End) GetDateRange(AdminRevenueRequestDto req)
    {
        var now = DateTime.UtcNow;
        DateTime start, end;

        switch (req.Range)
        {
            case AdminRevenueRange.Today:
                start = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                end = start.AddDays(1);
                break;

            case AdminRevenueRange.Week:
                var weekBase = now.Date;
                var diff = (7 + (int)weekBase.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                start = DateTime.SpecifyKind(weekBase.AddDays(-diff), DateTimeKind.Utc);
                end = start.AddDays(7);
                break;

            case AdminRevenueRange.Month:
                start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(1);
                break;

            case AdminRevenueRange.Quarter:
                var quarter = (now.Month - 1) / 3 + 1;
                start = new DateTime(now.Year, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(3);
                break;

            case AdminRevenueRange.Year:
                start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddYears(1);
                break;

            case AdminRevenueRange.Custom:
                start = req.StartDate.HasValue
                    ? DateTime.SpecifyKind(req.StartDate.Value.Date, DateTimeKind.Utc)
                    : DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                end = req.EndDate.HasValue
                    ? DateTime.SpecifyKind(req.EndDate.Value.Date, DateTimeKind.Utc).AddDays(1)
                    : start.AddDays(1);
                break;

            default:
                start = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                end = start.AddDays(1);
                break;
        }

        return (start, end);
    }

    private (DateTime Start, DateTime End) GetPreviousDateRange(AdminRevenueRange range, DateTime start, DateTime end)
    {
        var period = end - start;
        return range switch
        {
            AdminRevenueRange.Today => (start.AddDays(-1), start),
            AdminRevenueRange.Week => (start.AddDays(-7), start),
            AdminRevenueRange.Month => (start.AddMonths(-1), start),
            AdminRevenueRange.Quarter => (start.AddMonths(-3), start),
            AdminRevenueRange.Year => (start.AddYears(-1), start),
            AdminRevenueRange.Custom => (start - period, start),
            _ => (start.AddDays(-1), start)
        };
    }

    #endregion

    private async Task<PlatformTimeSeriesDto> BuildTimeSeriesDataAsync(
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        var categories = new List<string>();
        var platformRevenue = new List<decimal>();
        var grossSales = new List<decimal>();
        var payoutCounts = new List<int>();

        var totalDays = (end - start).Days;

        for (var day = 0; day < totalDays; day++)
        {
            var date = start.AddDays(day);
            categories.Add(date.ToString("dd/MM"));

            // Platform revenue for this day
            var dailyRevenue = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.Status == PayoutStatus.COMPLETED
                            && p.CompletedAt.HasValue
                            && p.CompletedAt.Value.Date == date.Date)
                .SumAsync(p => (decimal?)p.PlatformFeeAmount, ct) ?? 0m;

            platformRevenue.Add(dailyRevenue);

            // Gross sales for this day
            var dailySales = await _unitOfWork.Orders.GetQueryable()
                .Where(o => o.Status == OrderStatus.COMPLETED.ToString()
                            && o.CompletedAt.HasValue
                            && o.CompletedAt.Value.Date == date.Date
                            && !o.IsDeleted)
                .Include(o => o.OrderDetails)
                .SelectMany(o => o.OrderDetails)
                .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
                .SumAsync(od => od.FinalDetailPrice ?? od.TotalPrice, ct);

            grossSales.Add(dailySales);

            // Payout count for this day
            var dailyPayouts = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.Status == PayoutStatus.COMPLETED
                            && p.CompletedAt.HasValue
                            && p.CompletedAt.Value.Date == date.Date)
                .CountAsync(ct);

            payoutCounts.Add(dailyPayouts);
        }

        return new PlatformTimeSeriesDto
        {
            Categories = categories,
            PlatformRevenue = platformRevenue,
            GrossSales = grossSales,
            PayoutCounts = payoutCounts
        };
    }

    public async Task<SellerStatisticsDto> GetSellerStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        // Active sellers (those with completed payouts in period)
        var activeSellers = await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.Status == PayoutStatus.COMPLETED
                        && p.CompletedAt >= startDate
                        && p.CompletedAt < endDate)
            .Select(p => p.SellerId)
            .Distinct()
            .CountAsync(ct);

        // Total registered sellers
        var totalSellers = await _unitOfWork.Sellers.GetQueryable()
            .Where(s => !s.IsDeleted)
            .CountAsync(ct);

        // Top performing sellers by revenue
        var topSellers = await _unitOfWork.Payouts.GetQueryable()
            .Where(p => p.Status == PayoutStatus.COMPLETED
                        && p.CompletedAt >= startDate
                        && p.CompletedAt < endDate)
            .Include(p => p.Seller)
            .ThenInclude(s => s.User)
            .GroupBy(p => new { p.SellerId, p.Seller })
            .Select(g => new TopSellerDto
            {
                SellerId = g.Key.SellerId,
                SellerName = g.Key.Seller.CompanyName ?? g.Key.Seller.User.FullName ?? "Unknown",
                TotalRevenue = g.Sum(p => p.GrossAmount),
                PlatformFeeGenerated = g.Sum(p => p.PlatformFeeAmount),
                PayoutCount = g.Count()
            })
            .OrderByDescending(s => s.PlatformFeeGenerated)
            .Take(10)
            .ToListAsync(ct);

        return new SellerStatisticsDto
        {
            ActiveSellers = activeSellers,
            TotalSellers = totalSellers,
            TopSellers = topSellers,
            PeriodStart = startDate,
            PeriodEnd = endDate
        };
    }
}