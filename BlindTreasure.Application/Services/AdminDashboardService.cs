using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.AdminStatisticDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggerService _loggerService;

        public AdminDashboardService(IUnitOfWork unitOfWork, ILoggerService loggerService)
        {
            _unitOfWork = unitOfWork;
            _loggerService = loggerService;
        }

        public async Task<AdminDashBoardDtos> GetDashboardAsync(AdminDashboardRequestDto req)
        {
            var (periodStart, periodEnd) = GetPeriodRange(req);

            // Orders
            var orders = await _unitOfWork.Orders.GetQueryable()
                .Where(o => o.CreatedAt >= periodStart && o.CreatedAt < periodEnd && !o.IsDeleted)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Shipments)
                .Include(o => o.Seller)
                .ToListAsync();

            // Payouts
            var payouts = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.PeriodStart >= periodStart && p.PeriodEnd < periodEnd && !p.IsDeleted)
                .ToListAsync();

            // Sellers
            var sellers = await _unitOfWork.Sellers.GetQueryable()
                .Where(s => !s.IsDeleted && s.IsVerified)
                .ToListAsync();

            // Customers
            var customers = await _unitOfWork.Users.GetQueryable()
                .Where(u => !u.IsDeleted && u.RoleName == RoleType.Admin)
                .ToListAsync();

            // Categories
            var categories = await _unitOfWork.Categories.GetQueryable()
                .Where(c => !c.IsDeleted)
                .Include(c => c.Products)
                .ToListAsync();

            // ----------- DOANH THU THỰC TẾ (COMPLETED/PROCESSING) -----------
            var validPayouts = payouts
                .Where(p => p.Status == PayoutStatus.PROCESSING || p.Status == PayoutStatus.COMPLETED)
                .ToList();

            var totalGrossAmount = validPayouts.Sum(p => p.GrossAmount);
            var totalPlatformFee = validPayouts.Sum(p => p.PlatformFeeAmount);
            var totalNetAmount = validPayouts.Sum(p => p.NetAmount);
            var totalPayouts = validPayouts.Count;

            // ----------- DOANH THU ƯỚC TÍNH (PAID) -----------
            var paidOrders = orders
                .Where(o => o.Status == OrderStatus.PAID.ToString())
                .ToList();

            var paidOrderDetails = paidOrders
                .SelectMany(o => o.OrderDetails)
                .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
                .ToList();

            var estimatedGrossAmount = paidOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice);
            var estimatedPlatformFee = Math.Round(estimatedGrossAmount * 5.0m / 100m, 2);
            var estimatedNetAmount = estimatedGrossAmount - estimatedPlatformFee;
            var estimatedOrderCount = paidOrders.Count;

            // ----------- Revenue Summary DTO -----------
            var platformFeeRate = validPayouts.FirstOrDefault()?.PlatformFeeRate ?? 5.0m;
            var revenueTakingRate = totalGrossAmount != 0
                ? Math.Round(totalPlatformFee * 100 / totalGrossAmount, 2)
                : 0m;

            var (prevStart, prevEnd) = GetPreviousPeriodRange(req.Range, periodStart, periodEnd);
            var prevPayouts = await _unitOfWork.Payouts.GetQueryable()
                .Where(p => p.PeriodStart >= prevStart && p.PeriodEnd < prevEnd && !p.IsDeleted)
                .ToListAsync();
            var prevValidPayouts = prevPayouts
                .Where(p => p.Status == PayoutStatus.PROCESSING || p.Status == PayoutStatus.COMPLETED)
                .ToList();
            var prevRevenue = prevValidPayouts.Sum(p => p.NetAmount);
            var revenueGrowthPercent = prevRevenue != 0
                ? Math.Round((totalNetAmount - prevRevenue) * 100 / Math.Abs(prevRevenue), 2)
                : totalNetAmount > 0 ? 100m : 0m;

            var revenueSummary = new RevenueSummaryDto
            {
                TotalGrossAmount = totalGrossAmount,
                TotalPlatformFee = totalPlatformFee,
                TotalNetAmount = totalNetAmount,
                RevenueGrowthPercent = revenueGrowthPercent,
                PreviousPeriodRevenue = prevRevenue,
                PlatformFeeRate = platformFeeRate,
                RevenueTakingRate = revenueTakingRate,
                TotalPayouts = totalPayouts,
                EstimatedGrossAmount = estimatedGrossAmount,
                EstimatedPlatformFee = estimatedPlatformFee,
                EstimatedNetAmount = estimatedNetAmount,
                EstimatedOrderCount = estimatedOrderCount
            };

            // ----------- Order Summary DTO -----------
            var completedOrders = orders.Where(o => o.Status == OrderStatus.COMPLETED.ToString()).ToList();
            var completedOrderDetails = completedOrders.SelectMany(o => o.OrderDetails)
                .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
                .ToList();

            var pendingOrders = orders.Count(o => o.Status == OrderStatus.PENDING.ToString());
            var cancelledOrders = orders.Count(o => o.Status == OrderStatus.CANCELLED.ToString());
            var refundedOrders = orders.Count(o => o.Status == OrderStatus.REFUNDED.ToString());

            var shippingOrders = orders.Count(o =>
                o.Status == OrderStatus.PAID.ToString() &&
                o.ShippingAddressId != null &&
                o.OrderDetails.Any(od =>
                    od.Status == OrderDetailItemStatus.DELIVERING ||
                    (od.Shipments != null && od.Shipments.Any(s => s.Status != ShipmentStatus.COMPLETED))
                )
            );

            var deliveredOrders = orders.Count(o =>
                o.Status == OrderStatus.COMPLETED.ToString() &&
                o.OrderDetails.All(od =>
                    od.Status == OrderDetailItemStatus.DELIVERED &&
                    (od.Shipments == null || od.Shipments.All(s => s.Status == ShipmentStatus.COMPLETED))
                )
            );

            var inventoryOrders = orders.Count(o =>
                o.Status == OrderStatus.PAID.ToString() &&
                o.ShippingAddressId == null &&
                o.OrderDetails.Any(od => od.Status == OrderDetailItemStatus.IN_INVENTORY)
            );

            var averageOrderValue = completedOrders.Count > 0
                ? Math.Round(completedOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice) / completedOrders.Count, 2)
                : 0m;

            var totalItemsSold = completedOrderDetails.Sum(od => od.Quantity);

            var estimatedItemsSold = paidOrderDetails.Sum(od => od.Quantity);
            var estimatedAverageOrderValue = estimatedOrderCount > 0
                ? Math.Round(estimatedGrossAmount / estimatedOrderCount, 2)
                : 0m;

            var orderSummary = new OrderSummaryDto
            {
                TotalOrders = orders.Count,
                PendingOrders = pendingOrders,
                ShippingOrders = shippingOrders,
                InventoryOrders = inventoryOrders,
                DeliveredOrders = deliveredOrders,
                CancelledOrders = cancelledOrders,
                RefundedOrders = refundedOrders,
                OrderGrowthPercent = CalculateOrderGrowth(orders.Count, periodStart, periodEnd, req.Range),
                AverageOrderValue = averageOrderValue,
                TotalItemsSold = totalItemsSold,
                EstimatedOrders = estimatedOrderCount,
                EstimatedAverageOrderValue = estimatedAverageOrderValue,
                EstimatedItemsSold = estimatedItemsSold
            };

            // ----------- Seller Summary DTO -----------
            var activeSellerIds = completedOrders.Select(o => o.SellerId).Distinct().ToList();
            var estimatedActiveSellerIds = paidOrders.Select(o => o.SellerId).Distinct().ToList();

            var topSellers = validPayouts
                .GroupBy(p => p.SellerId)
                .Select(g =>
                {
                    var seller = sellers.FirstOrDefault(s => s.Id == g.Key);
                    return new TopSellerDto
                    {
                        SellerId = g.Key,
                        SellerName = seller?.CompanyName ?? seller?.User?.FullName ?? "",
                        TotalRevenue = g.Sum(p => p.GrossAmount),
                        PlatformFeeGenerated = g.Sum(p => p.PlatformFeeAmount),
                        PayoutCount = g.Count()
                    };
                })
                .OrderByDescending(ts => ts.TotalRevenue)
                .Take(5)
                .ToList();

            var estimatedTopSellers = paidOrders
                .GroupBy(o => o.SellerId)
                .Select(g =>
                {
                    var seller = sellers.FirstOrDefault(s => s.Id == g.Key);
                    var gross = g.SelectMany(o => o.OrderDetails)
                        .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
                        .Sum(od => od.FinalDetailPrice ?? od.TotalPrice);
                    var platformFee = Math.Round(gross * 5.0m / 100m, 2);
                    return new TopSellerDto
                    {
                        SellerId = g.Key,
                        SellerName = seller?.CompanyName ?? seller?.User?.FullName ?? "",
                        EstimatedRevenue = gross,
                        EstimatedPlatformFeeGenerated = platformFee,
                        EstimatedPayoutCount = g.Count()
                    };
                })
                .OrderByDescending(ts => ts.EstimatedRevenue)
                .Take(5)
                .ToList();

            var sellerSummary = new SellerSummaryDto
            {
                TotalSellers = sellers.Count,
                ActiveSellers = activeSellerIds.Count,
                TopSellers = topSellers,
                EstimatedActiveSellers = estimatedActiveSellerIds.Count,
                EstimatedTopSellers = estimatedTopSellers
            };

            // ----------- Customer Summary DTO -----------
            var newCustomersThisPeriod = customers.Count(u => u.CreatedAt >= periodStart && u.CreatedAt < periodEnd);
            var customerSummary = new CustomerSummaryDto
            {
                TotalCustomers = customers.Count,
                NewCustomersThisPeriod = newCustomersThisPeriod
            };

            // ----------- Category Revenue DTO -----------
            var topCategories = new List<CategoryRevenueDto>();

            foreach (var category in categories)
            {
                var products = category.Products ?? new List<Product>();

                // Completed
                var completedCategoryOrderDetails = completedOrderDetails
                    .Where(od => products.Any(p => p.Id == od.ProductId))
                    .ToList();

                var completedOrderCount = completedCategoryOrderDetails
                    .Select(od => od.OrderId)
                    .Distinct()
                    .Count();

                var completedRevenue = completedCategoryOrderDetails
                    .Sum(od => od.FinalDetailPrice ?? od.TotalPrice);

                // Estimated
                var estimatedCategoryOrderDetails = paidOrderDetails
                    .Where(od => products.Any(p => p.Id == od.ProductId))
                    .ToList();

                var estimatedCategoryOrderCount = estimatedCategoryOrderDetails
                    .Select(od => od.OrderId)
                    .Distinct()
                    .Count();

                var estimatedCategoryRevenue = estimatedCategoryOrderDetails
                    .Sum(od => od.FinalDetailPrice ?? od.TotalPrice);

                topCategories.Add(new CategoryRevenueDto
                {
                    CategoryName = category.Name,
                    OrderCount = completedOrderCount,
                    Revenue = completedRevenue,
                    EstimatedOrderCount = estimatedCategoryOrderCount,
                    EstimatedRevenue = estimatedCategoryRevenue
                });
            }
            topCategories = topCategories.OrderByDescending(cr => cr.Revenue).Take(5).ToList();

            // ----------- Time Series DTO -----------
            var days = (periodEnd - periodStart).Days;
            var timeSeries = new TimeSeriesDto
            {
                Categories = Enumerable.Range(0, days)
                    .Select(i => periodStart.AddDays(i).ToString("dd/MM"))
                    .ToList(),
                PlatformRevenue = new List<decimal>(),
                GrossSales = new List<decimal>(),
                PayoutCounts = new List<int>(),
                OrderCounts = new List<int>(),
                EstimatedPlatformRevenue = new List<decimal>(),
                EstimatedGrossSales = new List<decimal>(),
                EstimatedPayoutCounts = new List<int>(),
                EstimatedOrderCounts = new List<int>()
            };

            for (int i = 0; i < days; i++)
            {
                var dayStart = periodStart.AddDays(i);
                var dayEnd = dayStart.AddDays(1);

                var dayPayouts = validPayouts.Where(p => p.PeriodStart <= dayStart && p.PeriodEnd >= dayStart).ToList();
                var dayOrders = completedOrders.Where(o => o.CreatedAt >= dayStart && o.CreatedAt < dayEnd).ToList();
                var dayPaidOrders = paidOrders.Where(o => o.PlacedAt >= dayStart && o.PlacedAt < dayEnd).ToList();

                var dayPaidOrderDetails = dayPaidOrders.SelectMany(o => o.OrderDetails)
                    .Where(od => od.Status != OrderDetailItemStatus.CANCELLED)
                    .ToList();

                var dayGross = dayPayouts.Sum(p => p.GrossAmount);
                var dayPlatformFee = dayPayouts.Sum(p => p.PlatformFeeAmount);

                var dayEstimatedGross = dayPaidOrderDetails.Sum(od => od.FinalDetailPrice ?? od.TotalPrice);
                var dayEstimatedPlatformFee = Math.Round(dayEstimatedGross * 5.0m / 100m, 2);

                timeSeries.PlatformRevenue.Add(dayPlatformFee);
                timeSeries.GrossSales.Add(dayGross);
                timeSeries.PayoutCounts.Add(dayPayouts.Count);
                timeSeries.OrderCounts.Add(dayOrders.Count);

                timeSeries.EstimatedPlatformRevenue.Add(dayEstimatedPlatformFee);
                timeSeries.EstimatedGrossSales.Add(dayEstimatedGross);
                timeSeries.EstimatedPayoutCounts.Add(dayPaidOrders.Count);
                timeSeries.EstimatedOrderCounts.Add(dayPaidOrders.Count);
            }

            return new AdminDashBoardDtos
            {
                RevenueSummary = revenueSummary,
                OrderSummary = orderSummary,
                SellerSummary = sellerSummary,
                CustomerSummary = customerSummary,
                TopCategories = topCategories,
                TimeSeries = timeSeries,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private (DateTime Start, DateTime End) GetPeriodRange(AdminDashboardRequestDto req)
        {
            var now = DateTime.UtcNow;
            DateTime start, end;
            switch (req.Range)
            {
                case AdminDashboardRange.Today:
                    start = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                    end = start.AddDays(1);
                    break;
                case AdminDashboardRange.Week:
                    var diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    start = DateTime.SpecifyKind(now.Date.AddDays(-diff), DateTimeKind.Utc);
                    end = start.AddDays(7);
                    break;
                case AdminDashboardRange.Month:
                    start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    end = start.AddMonths(1);
                    break;
                case AdminDashboardRange.Quarter:
                    var quarter = (now.Month - 1) / 3 + 1;
                    start = new DateTime(now.Year, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    end = start.AddMonths(3);
                    break;
                case AdminDashboardRange.Year:
                    start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    end = start.AddYears(1);
                    break;
                case AdminDashboardRange.Custom:
                    start = req.StartDate?.Date != null
                        ? DateTime.SpecifyKind(req.StartDate.Value.Date, DateTimeKind.Utc)
                        : DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                    end = req.EndDate?.Date != null
                        ? DateTime.SpecifyKind(req.EndDate.Value.Date, DateTimeKind.Utc).AddDays(1)
                        : start.AddDays(1);
                    if (end <= start) end = start.AddDays(1);
                    break;
                default:
                    start = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                    end = start.AddDays(1);
                    break;
            }
            return (start, end);
        }

        private (DateTime Start, DateTime End) GetPreviousPeriodRange(AdminDashboardRange range, DateTime start, DateTime end)
        {
            var period = end - start;
            return range switch
            {
                AdminDashboardRange.Today => (start.AddDays(-1), start),
                AdminDashboardRange.Week => (start.AddDays(-7), start),
                AdminDashboardRange.Month => (start.AddMonths(-1), start),
                AdminDashboardRange.Quarter => (start.AddMonths(-3), start),
                AdminDashboardRange.Year => (start.AddYears(-1), start),
                AdminDashboardRange.Custom => (start - period, start),
                _ => (start.AddDays(-1), start)
            };
        }

        private decimal CalculateOrderGrowth(int currentOrderCount, DateTime periodStart, DateTime periodEnd, AdminDashboardRange range)
        {
            var (prevStart, prevEnd) = GetPreviousPeriodRange(range, periodStart, periodEnd);
            var prevOrderCount = _unitOfWork.Orders.GetQueryable()
                .Count(o => o.CreatedAt >= prevStart && o.CreatedAt < prevEnd && !o.IsDeleted);
            return prevOrderCount != 0
                ? Math.Round((decimal)(currentOrderCount - prevOrderCount) * 100 / prevOrderCount, 2)
                : currentOrderCount > 0 ? 100m : 0m;
        }
    }
}