namespace BlindTreasure.Domain.Entities;

public class Shipment : BaseEntity
{
    // FK → OrderDetail
    public Guid? OrderDetailId { get; set; }
    public OrderDetail? OrderDetail { get; set; }

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
    public string Status { get; set; }

    // 1-n → InventoryItems
    public ICollection<InventoryItem>? InventoryItems { get; set; }
}