using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class OrderDetail : BaseEntity
{
    // FK → Order
    public Guid OrderId { get; set; }
    public Order Order { get; set; }

    // Có thể là Product hoặc BlindBox
    public Guid? ProductId { get; set; }

    public Product? Product { get; set; }

    //
    public Guid? BlindBoxId { get; set; }
    public BlindBox? BlindBox { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? DetailDiscountPromotion { get; set; }
    public decimal? FinalDetailPrice { get; set; } // TotalPrice - DetailDiscountPromotion ?? 0;
    public OrderDetailItemStatus Status { get; set; }
    public string? Logs { get; set; } = string.Empty; // Lưu trữ nhật ký trạng thái


    // Tách rõ SellerId ở đây
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    // Many → Shipments
    public ICollection<Shipment>? Shipments { get; set; } = new List<Shipment>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<CustomerBlindBox>? CustomerBlindBoxes { get; set; } = new List<CustomerBlindBox>();
    public ICollection<Review>? Reviews { get; set; } = new List<Review>();
}