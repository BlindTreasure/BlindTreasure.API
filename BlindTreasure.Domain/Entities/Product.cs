using System.ComponentModel.DataAnnotations.Schema;
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

    public decimal RealSellingPrice { get; set; }
    public decimal? ListedPrice { get; set; } // Đây là giá niêm yết, có thể khác với giá bán thực tế

    public int TotalStockQuantity { get; set; }
    public int ReservedInBlindBox { get; set; }      
    [NotMapped]
    public int AvailableToSell => TotalStockQuantity - ReservedInBlindBox;
    public List<string> ImageUrls { get; set; } = new(); // new: khởi tạo mặc định tránh null
    public ProductStatus Status { get; set; }

    // thông tin chi tiết sản phẩm thực tế để có thể đi tính phí dịch vụ ghn
    public decimal? Length { get; set; } = 15; // cm
    public decimal? Weight { get; set; } = 15; // cm
    public decimal? Width { get; set; } = 10; //cm
    public decimal? Height { get; set; } = 5; // cm 
    public string? Material { get; set; }
    public ProductSaleType? ProductType { get; set; }
    public string? Brand { get; set; }

    // Navigation
    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<BlindBoxItem> BlindBoxItems { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; }
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public virtual ICollection<CustomerFavourite> CustomerFavourites { get; set; } = new List<CustomerFavourite>();
}