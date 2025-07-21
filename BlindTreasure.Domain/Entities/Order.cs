namespace BlindTreasure.Domain.Entities;

public class Order : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    public Guid? SellerId { get; set; } // Seller của đơn hàng, có thể là null nếu không có seller

    // 1-1 hoặc 1-n tuỳ config Fluent API
    public Guid? PaymentId { get; set; }
    public Payment Payment { get; set; }

    // FK → Address
    public Guid? ShippingAddressId { get; set; }
    public Address ShippingAddress { get; set; }

    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? FinalAmount { get; set; } = 0;
    public DateTime PlacedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Promotion? Promotion { get; set; } // Promotion áp dụng cho đơn hàng, có thể là null nếu không có
    // 1-n → OrderDetails
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<OrderSellerPromotion> OrderSellerPromotions { get; set; }

}