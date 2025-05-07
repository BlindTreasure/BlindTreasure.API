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

    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime PlacedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // 1-n → OrderDetails
    public ICollection<OrderDetail> OrderDetails { get; set; }
}