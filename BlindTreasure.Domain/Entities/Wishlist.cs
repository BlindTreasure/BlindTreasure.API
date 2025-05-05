namespace BlindTreasure.Domain.Entities;

using System.ComponentModel.DataAnnotations;

public class Wishlist : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
    public int ItemCount { get; set; }

    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
}
