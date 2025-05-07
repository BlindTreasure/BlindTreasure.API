namespace BlindTreasure.Domain.Entities;

public class Shipment : BaseEntity
{
    // FK → OrderDetail
    public Guid OrderDetailId { get; set; }
    public OrderDetail OrderDetail { get; set; }

    public string Provider { get; set; }
    public string TrackingNumber { get; set; }
    public DateTime ShippedAt { get; set; }
    public DateTime EstimatedDelivery { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string Status { get; set; }
}