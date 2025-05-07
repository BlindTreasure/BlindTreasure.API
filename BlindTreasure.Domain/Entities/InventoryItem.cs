namespace BlindTreasure.Domain.Entities;

public class InventoryItem : BaseEntity
{
    // FK → User (chủ sở hữu sau unbox)
    public Guid UserId { get; set; }
    public User User { get; set; }

    // FK → Product
    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int RestockThreshold { get; set; }
    public string Location { get; set; }
    public string Status { get; set; }

    // 1-n → Listings
    public ICollection<Listing> Listings { get; set; }
}