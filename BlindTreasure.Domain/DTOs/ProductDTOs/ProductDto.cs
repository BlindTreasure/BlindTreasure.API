using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ProductDTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public decimal? Height { get; set; }
    public string? Material { get; set; }
    public ProductSaleType? ProductType { get; set; }
    public string? Brand { get; set; }

    public ProductStatus Status { get; set; }

    public List<string>? ImageUrls { get; set; } 

    public Guid SellerId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}