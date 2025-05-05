namespace BlindTreasure.Domain.Entities;

public class Cart : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime LastUpdated { get; set; }

    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<Order> Orders { get; set; }
}