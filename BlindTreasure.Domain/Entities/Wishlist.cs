namespace BlindTreasure.Domain.Entities;

public class Wishlist : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsDefault { get; set; }
    public int ItemCount { get; set; }

    // 1-n → WishlistItems
    public ICollection<WishlistItem> WishlistItems { get; set; }
}