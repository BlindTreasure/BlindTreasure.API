namespace BlindTreasure.Domain.Entities;

public class Wishlist : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsDefault { get; set; }
    public int ItemCount { get; set; }

    public ICollection<WishlistItem> WishlistItems { get; set; }
}