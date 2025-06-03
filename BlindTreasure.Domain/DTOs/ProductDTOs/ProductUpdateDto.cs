using BlindTreasure.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.DTOs.ProductDTOs;

public class ProductUpdateDto 
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? Price { get; set; }
    public int? Stock { get; set; }
    //public ProductStatus? Status { get; set; }
    public decimal? Height { get; set; } // cm 
    public string? Material { get; set; }
    public ProductSaleType? ProductType { get; set; }
    public string? Brand { get; set; }
}

