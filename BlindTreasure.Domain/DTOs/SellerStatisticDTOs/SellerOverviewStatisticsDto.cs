using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

public class SellerOverviewStatisticsDto
{
    // Estimated Revenue (PAID orders)
    public decimal EstimatedRevenue { get; set; }
    public decimal EstimatedRevenueLastPeriod { get; set; }
    public decimal EstimatedRevenueGrowthPercent { get; set; }


    public decimal ActualRevenue { get; set; }
    public decimal ActualRevenueLastPeriod { get; set; }
    public decimal ActualRevenueGrowthPercent { get; set; }

    public int TotalOrders { get; set; }
    public int TotalOrdersLastPeriod { get; set; }
    public decimal OrdersGrowthPercent { get; set; }

    public int TotalProductsSold { get; set; }
    public int TotalProductsSoldLastPeriod { get; set; }
    public decimal ProductsSoldGrowthPercent { get; set; }

    public decimal AverageOrderValue { get; set; }
    public decimal AverageOrderValueLastPeriod { get; set; }
    public decimal AverageOrderValueGrowthPercent { get; set; }

    // Thống kê theo thời gian
    public SellerStatisticsResponseDto TimeSeriesData { get; set; } = new();
}