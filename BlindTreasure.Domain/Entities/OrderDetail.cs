using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class OrderDetail : BaseEntity
{
    // FK → Order
    public Guid OrderId { get; set; }
    public Order Order { get; set; }

    // Có thể là Product hoặc BlindBox
    public Guid? ProductId { get; set; } = null;
    public Product Product { get; set; } = null;
    //
    public Guid? BlindBoxId { get; set; } = null;
    public BlindBox BlindBox { get; set; } = null;
    public int? TotalShippingFee { get; set; } = null; // Tổng phí vận chuyển



    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public OrderDetailStatus Status { get; set; } 


    // Tách rõ SellerId ở đây
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    // 1-n → Shipments
    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<CustomerBlindBox>? CustomerBlindBoxes { get; set; } = new List<CustomerBlindBox>();
}