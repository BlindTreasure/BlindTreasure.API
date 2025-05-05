namespace BlindTreasure.Domain.Entities;

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; }

    public Guid? ProductId { get; set; }
    public Product Product { get; set; }

    public Guid? BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime AddedAt { get; set; }
}