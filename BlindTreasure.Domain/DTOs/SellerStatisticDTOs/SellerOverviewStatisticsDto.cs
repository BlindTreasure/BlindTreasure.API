using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

public class SellerOverviewStatisticsDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalRevenueLastPeriod { get; set; }
    public decimal RevenueGrowthPercent { get; set; }

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