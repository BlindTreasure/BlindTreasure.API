namespace BlindTreasure.Domain.Entities;

public class Order : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public Guid CartId { get; set; }
    public Cart Cart { get; set; }

    public string Status { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid? PaymentId { get; set; }
    public Payment Payment { get; set; }

    public Guid? ShippingAddressId { get; set; }
    public Address ShippingAddress { get; set; }

    public DateTime PlacedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; }
}