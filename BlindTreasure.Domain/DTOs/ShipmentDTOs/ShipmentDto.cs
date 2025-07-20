using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs;

public class ShipmentDto
{
    public OrderDetailDto? OrderDetail { get; set; }


    //các field cho GHN 
    public string? OrderCode { get; set; } // Mã đơn hàng của GHN    
    public int? TotalFee { get; set; } // Tổng phí vận chuyển

    public int? MainServiceFee { get; set; } // phí dịch vụ

    //

    public string? Provider { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? EstimatedDelivery { get; set; } //expected delivery date
    public DateTime? DeliveredAt { get; set; }
    public string? Status { get; set; }
}