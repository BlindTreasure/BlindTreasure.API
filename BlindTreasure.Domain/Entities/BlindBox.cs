using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class BlindBox : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }

    public bool HasSecretItem { get; set; }
    public decimal SecretProbability { get; set; } = 0.05m; // mặc định 5%
    public BlindBoxStatus Status { get; set; }

    public string ImageUrl { get; set; }
    public DateTime ReleaseDate { get; set; }

    public ICollection<BlindBoxItem> BlindBoxItems { get; set; }
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<WishlistItem> WishlistItems { get; set; }
}