using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.AdminStatisticDTOs;

// Request DTO for dashboard queries
public class AdminDashboardRequestDto
{
    public AdminDashboardRange Range { get; set; } = AdminDashboardRange.Month;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public enum AdminDashboardRange
{
    Today = 1,
    Week = 2,
    Month = 3,
    Quarter = 4,
    Year = 5,
    Custom = 6
}

// Main dashboard response for admin
public class AdminDashBoardDtos
{
    public RevenueSummaryDto RevenueSummary { get; set; } = new();
    public OrderSummaryDto OrderSummary { get; set; } = new();
    public SellerSummaryDto SellerSummary { get; set; } = new();
    public CustomerSummaryDto CustomerSummary { get; set; } = new();
    public List<CategoryRevenueDto> TopCategories { get; set; } = new();
    public TimeSeriesDto TimeSeries { get; set; } = new();
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
}

// Revenue overview
public class RevenueSummaryDto
{
    public decimal TotalGrossAmount { get; set; }
    public decimal TotalPlatformFee { get; set; }
    public decimal TotalNetAmount { get; set; }
    public decimal RevenueGrowthPercent { get; set; }
    public decimal PreviousPeriodRevenue { get; set; }
    public decimal PlatformFeeRate { get; set; } = 5.0m;
    public decimal RevenueTakingRate { get; set; } // PlatformFee / Gross * 100
    public int TotalPayouts { get; set; }

    // Estimated fields
    public decimal EstimatedGrossAmount { get; set; }
    public decimal EstimatedPlatformFee { get; set; }
    public decimal EstimatedNetAmount { get; set; }
    public int EstimatedOrderCount { get; set; }
}

// Order overview
public class OrderSummaryDto
{
    public int TotalOrders { get; set; }

    public int PendingOrders { get; set; }

    //public int ConfirmedOrders { get; set; } không tồn tại
    //public int ProcessingOrders { get; set; } không tồn tại
    public int ShippingOrders { get; set; } // kho order có shipping addressed + completed
    public int DeliveredOrders { get; set; } // order-details của order đó có trạng thái delivered
    public int CancelledOrders { get; set; }
    public int InventoryOrders { get; set; } // order-details của order đó có trạng thái IN_INVENTORY
    public int RefundedOrders { get; set; }
    public decimal OrderGrowthPercent { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalItemsSold { get; set; }

    // Estimated fields
    public int EstimatedOrders { get; set; }
    public decimal EstimatedAverageOrderValue { get; set; }
    public int EstimatedItemsSold { get; set; }
}

// Seller overview
public class SellerSummaryDto
{
    public int TotalSellers { get; set; }
    public int ActiveSellers { get; set; }
    public List<TopSellerDto> TopSellers { get; set; } = new();

    // Estimated fields
    public int EstimatedActiveSellers { get; set; }
    public List<TopSellerDto> EstimatedTopSellers { get; set; } = new();
}

public class TopSellerDto
{
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal PlatformFeeGenerated { get; set; }
    public int PayoutCount { get; set; }

    // Estimated fields
    public decimal EstimatedRevenue { get; set; }
    public decimal EstimatedPlatformFeeGenerated { get; set; }
    public int EstimatedPayoutCount { get; set; }
}

// Customer overview
public class CustomerSummaryDto
{
    public int TotalCustomers { get; set; }
    public int NewCustomersThisPeriod { get; set; }
}

// Category revenue breakdown
public class CategoryRevenueDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }

    // Estimated fields
    public int EstimatedOrderCount { get; set; }
    public decimal EstimatedRevenue { get; set; }
}

// Time series for charts
public class TimeSeriesDto
{
    public List<string> Categories { get; set; } = new(); // Date labels
    public List<decimal> PlatformRevenue { get; set; } = new();
    public List<decimal> GrossSales { get; set; } = new(); 
    public List<int> PayoutCounts { get; set; } = new();
    public List<int> OrderCounts { get; set; } = new();

    // Estimated fields
    public List<decimal> EstimatedPlatformRevenue { get; set; } = new();
    public List<decimal> EstimatedGrossSales { get; set; } = new();
    public List<int> EstimatedPayoutCounts { get; set; } = new();
    public List<int> EstimatedOrderCounts { get; set; } = new();
}