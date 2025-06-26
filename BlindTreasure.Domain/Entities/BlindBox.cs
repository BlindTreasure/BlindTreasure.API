using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class BlindBox : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller? Seller { get; set; }
    public string? BindBoxTags { get; set; } // JSON string or array of tags
    public required string Name { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public bool HasSecretItem { get; set; }
    public int SecretProbability { get; set; }
    public BlindBoxStatus Status { get; set; }
    public string? Brand { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string? RejectReason { get; set; }
    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }
    public ICollection<BlindBoxItem>? BlindBoxItems { get; set; }
    public ICollection<CustomerInventory>? CustomerInventories { get; set; }
    public ICollection<CartItem>? CartItems { get; set; }
    public ICollection<OrderDetail>? OrderDetails { get; set; }
    public ICollection<Review>? Reviews { get; set; }
    public ICollection<WishlistItem>? WishlistItems { get; set; }
}