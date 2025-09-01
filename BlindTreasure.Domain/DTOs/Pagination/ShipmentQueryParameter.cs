using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination
{
    public class ShipmentQueryParameter : PaginationParameter
    {
        public string? Search { get; set; } // search ORDER CODE CỦA SHIPMENT
        public ShipmentStatus? Status { get; set; }
        public int? MinTotalFee { get; set; }
        public int? MaxTotalFee { get; set; } 
        public DateTime? FromEstimatedPickupTime { get; set; } 

        public DateTime? ToEstimatedPickupTime { get; set; }
    }
}
