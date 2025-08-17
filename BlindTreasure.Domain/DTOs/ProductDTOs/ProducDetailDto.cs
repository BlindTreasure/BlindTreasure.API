using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ProductDTOs;

public class ProducDetailDto
{
    // Primary Key
    public Guid Id { get; set; }
    
    // Basic Product Information
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    
    // Category & Seller
    public Guid CategoryId { get; set; }
    public Guid SellerId { get; set; }
    
    // Pricing
    public decimal Price { get; set; }
    
    // Stock Management (grouped together)
    public int TotalStockQuantity { get; set; }
    public int ReservedInBlindBox { get; set; }
    public int AvailableToSell { get; set; }
    public StockStatus ProductStockStatus { get; set; }
    
    // Product Specifications
    public decimal? Height { get; set; }
    public string? Material { get; set; }
    public ProductSaleType? ProductType { get; set; }
    
    // Status & Media
    public ProductStatus Status { get; set; }
    public List<string>? ImageUrls { get; set; }
    
    // Audit Fields
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}