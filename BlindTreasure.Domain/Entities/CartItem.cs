namespace BlindTreasure.Domain.Entities;

public class CartItem : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    // Có thể là Product hoặc BlindBox
    public Guid? ProductId { get; set; }
    public Product Product { get; set; }
    public Guid? BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime AddedAt { get; set; }
}