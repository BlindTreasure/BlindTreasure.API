namespace BlindTreasure.Domain.Entities;

public class OrderDetail : BaseEntity
{
    // FK → Order
    public Guid OrderId { get; set; }
    public Order Order { get; set; }

    // Có thể là Product hoặc BlindBox
    public Guid? ProductId { get; set; }
    public Product Product { get; set; }
    public Guid? BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    // 1-n → Shipments
    public ICollection<Shipment> Shipments { get; set; }
    public ICollection<CustomerInventory>? CustomerInventories { get; set; }

}