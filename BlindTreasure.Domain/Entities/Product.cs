using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Product : BaseEntity
{
    // FK → Seller
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }

    // FK → Category
    public Guid CategoryId { get; set; }
    public Category Category { get; set; }

    public decimal Price { get; set; }
    public int Stock { get; set; }
    public List<string> ImageUrls { get; set; } = new(); // new: khởi tạo mặc định tránh null
    public ProductStatus Status { get; set; }

    public decimal? Height { get; set; } // cm 
    public string? Material { get; set; }
    public ProductSaleType? ProductType { get; set; }
    public string? Brand { get; set; }

    // Navigation
    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<BlindBoxItem> BlindBoxItems { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; }
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<WishlistItem> WishlistItems { get; set; }
    public ICollection<Review> Reviews { get; set; }
}