using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Shipment : BaseEntity
{
    // Many → OrderDetail
    public ICollection<OrderDetail>? OrderDetails { get; set; } = new List<OrderDetail>();

    //các field cho GHN 
    public string? OrderCode { get; set; } // Mã đơn hàng của GHN    
    public int? TotalFee { get; set; } // Tổng phí vận chuyển

    public int? MainServiceFee { get; set; } // phí dịch vụ

    //

    public string Provider { get; set; }
    public string TrackingNumber { get; set; }
    public DateTime ShippedAt { get; set; }
    public DateTime EstimatedDelivery { get; set; } //expected delivery date
    public DateTime? DeliveredAt { get; set; }
    public ShipmentStatus Status { get; set; }

    // 1-n → InventoryItems
    public ICollection<InventoryItem>? InventoryItems { get; set; }
}