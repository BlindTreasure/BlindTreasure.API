using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.Entities;

public class WishlistItem : BaseEntity
{
    public Guid WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = default!;

    public Guid? ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; } = default!;

    public DateTime AddedAt { get; set; }
    
    [MaxLength(255)]
    public string? Note { get; set; } = string.Empty;
}
