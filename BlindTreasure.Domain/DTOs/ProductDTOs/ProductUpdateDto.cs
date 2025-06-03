using BlindTreasure.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.DTOs.ProductDTOs;

public class ProductUpdateDto 
{
    [Required] [MaxLength(100)] public string Name { get; set; }

    [Required] [MaxLength(255)] public string Description { get; set; }

    [Required] public Guid CategoryId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Required] [Range(0, int.MaxValue)] public int Stock { get; set; }

    [Required] public ProductStatus Status { get; set; }

    public decimal? Height { get; set; } // cm 
    public string? Material { get; set; }
    public ProductSaleType? ProductType { get; set; }
    public string? Brand { get; set; }

}

