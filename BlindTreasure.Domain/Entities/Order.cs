namespace BlindTreasure.Domain.Entities;

public class Order : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }


    // 1-1 hoặc 1-n tuỳ config Fluent API
    public Guid? PaymentId { get; set; }
    public Payment Payment { get; set; }

    // FK → Address
    public Guid? ShippingAddressId { get; set; }
    public Address ShippingAddress { get; set; }

    public decimal? TotalShippingFee { get; set; } = 0; // Tổng phí vận chuyển của từ shipment của các items thuộc Order
    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? FinalAmount { get; set; } = 0;
    public DateTime PlacedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    //refund fields
    public decimal? TotalRefundAmount { get; set; }
    public string? RefundReason { get; set; }

    // Tách rõ SellerId ở đây
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    // MỚI: Group ID để nhóm các order cùng checkout
    public Guid? CheckoutGroupId { get; set; }

    //Payout tracking
    public Guid? PayoutId { get; set; } // FK to track which payout this order belongs to
    public Payout? Payout { get; set; }

    //promotion
    public ICollection<OrderSellerPromotion> OrderSellerPromotions { get; set; }

    // 1-n → OrderDetails
    public ICollection<OrderDetail> OrderDetails { get; set; }
}