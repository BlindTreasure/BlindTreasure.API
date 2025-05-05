namespace BlindTreasure.Domain.Entities;

public class Product : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; }

    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string ImageUrl { get; set; }
    public string Status { get; set; }

    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<BlindBoxItem> BlindBoxItems { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<WishlistItem> WishlistItems { get; set; }
}