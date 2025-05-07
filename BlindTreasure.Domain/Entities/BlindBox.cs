namespace BlindTreasure.Domain.Entities;

public class BlindBox : BaseEntity
{
    // FK → Seller
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public string ProbabilityConfig { get; set; }
    public bool HasSecretItem { get; set; }
    public decimal SecretProbability { get; set; }
    public string ImageUrl { get; set; }
    public DateTime ReleaseDate { get; set; }

    // 1-n → BlindBoxItems, CartItems, OrderDetails, Reviews, WishlistItems
    public ICollection<BlindBoxItem> BlindBoxItems { get; set; }
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<WishlistItem> WishlistItems { get; set; }
}