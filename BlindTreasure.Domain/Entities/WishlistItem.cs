namespace BlindTreasure.Domain.Entities;

public class WishlistItem : BaseEntity
{
    // FK → Wishlist
    public Guid WishlistId { get; set; }
    public Wishlist Wishlist { get; set; }

    public Guid? ProductId { get; set; }
    public Product Product { get; set; }
    public Guid? BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    public DateTime AddedAt { get; set; }
    public string Note { get; set; }
}