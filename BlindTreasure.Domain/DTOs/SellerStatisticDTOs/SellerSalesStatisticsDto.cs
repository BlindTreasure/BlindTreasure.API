using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs
{
    public class SellerSalesStatisticsDto
    {
        public Guid SellerId { get; set; }
        public int TotalOrders { get; set; }
        public int TotalProductsSold { get; set; }
        public decimal GrossSales { get; set; }
        public decimal NetSales { get; set; }
        public decimal TotalRefunded { get; set; }
        public decimal TotalDiscount { get; set; }
    }
}
