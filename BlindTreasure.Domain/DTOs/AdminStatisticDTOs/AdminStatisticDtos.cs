using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.AdminStatisticDTOs
{

    #region Request DTOs

    public class AdminRevenueRequestDto
    {
        public AdminRevenueRange Range { get; set; } = AdminRevenueRange.Month;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public enum AdminRevenueRange
    {
        Today = 1,
        Week = 2,
        Month = 3,
        Quarter = 4,
        Year = 5,
        Custom = 6
    }

    #endregion

    #region Dashboard Response DTOs

    public class PlatformRevenueDashboardDto
    {
        public PlatformRevenueDto PlatformRevenue { get; set; } = new();
        public GrossSalesDto GrossSales { get; set; } = new();
        public SellerStatisticsDto SellerStatistics { get; set; } = new();
        public decimal RevenueGrowthPercent { get; set; }
        public decimal PreviousPeriodRevenue { get; set; }
        public PlatformTimeSeriesDto TimeSeries { get; set; } = new();
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime GeneratedAt { get; set; }

    }

    #endregion

    #region Core Statistics DTOs

    public class PlatformRevenueDto
    {
        public decimal TotalRevenue { get; set; }           // Platform fees collected
        public int TotalPayouts { get; set; }               // Number of completed payouts
        public decimal TotalGrossAmount { get; set; }       // Total gross amount from payouts
        public decimal AveragePayoutAmount { get; set; }    // Average payout amount
        public int TransactionCount { get; set; }           // Number of payout transactions
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class GrossSalesDto
    {
        public decimal TotalSales { get; set; }             // Total sales from completed orders
        public int TotalOrders { get; set; }                // Number of completed orders
        public int TotalItemsSold { get; set; }             // Total quantity sold
        public decimal AverageOrderValue { get; set; }      // Average order value
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class SellerStatisticsDto
    {
        public int ActiveSellers { get; set; }              // Sellers with payouts in period
        public int TotalSellers { get; set; }               // All registered sellers
        public List<TopSellerDto> TopSellers { get; set; } = new();
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }


    #endregion

    #region Time Series DTOs

    public class PlatformTimeSeriesDto
    {
        public List<string> Categories { get; set; } = new();          // Date labels (dd/MM format)
        public List<decimal> PlatformRevenue { get; set; } = new();    // Daily platform revenue
        public List<decimal> GrossSales { get; set; } = new();         // Daily gross sales
        public List<int> PayoutCounts { get; set; } = new();           // Daily payout counts
    }

    #endregion

    #region Summary DTOs

    public class PlatformRevenueSummaryDto
    {
        public decimal TotalPlatformRevenue { get; set; }
        public decimal TotalGrossSales { get; set; }
        public decimal PlatformFeeRate { get; set; } = 5.0m;
        public decimal RevenueTakingRate { get; set; }       // Platform revenue / Gross sales * 100
        public int TotalCompletedPayouts { get; set; }
        public int TotalCompletedOrders { get; set; }
        public int ActiveSellersCount { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    #endregion


    //public class TopSellerDto
    //{
    //    public Guid SellerId { get; set; }
    //    public string SellerName { get; set; } = string.Empty;
    //    public decimal TotalRevenue { get; set; }           // Seller's gross revenue
    //    public decimal PlatformFeeGenerated { get; set; }   // Platform fees from this seller
    //    public int PayoutCount { get; set; }                // Number of payouts
    //}
}


