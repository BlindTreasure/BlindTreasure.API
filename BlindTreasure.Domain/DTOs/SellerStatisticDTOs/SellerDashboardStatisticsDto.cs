using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs
{
    public class SellerDashboardStatisticsDto
    {
        public SellerOverviewStatisticsDto Overview { get; set; } = new();
        public List<TopSellingProductDto> TopProducts { get; set; } = new();
        public List<TopSellingBlindBoxDto> TopBlindBoxes { get; set; } = new();
        public List<OrderStatusStatisticsDto> OrderStatusStats { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
