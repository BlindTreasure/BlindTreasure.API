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
    public string Location { get; set; } = "HCM"; // Vị trí kho, mặc định là "HCM"
    public string Status { get; set; }

    // 1-n → Listings
    public ICollection<Listing> Listings { get; set; }
}