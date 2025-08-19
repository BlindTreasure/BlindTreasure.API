using System.Text.Json.Serialization;
using BlindTreasure.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.ProductDTOs;

public class ProductUpdateDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? RealSellingPrice { get; set; }
    public decimal? ListedPrice { get; set; } // Đây là giá niêm yết, có thể khác với giá bán thực tế


    public int? TotalStockQuantity { get; set; }

    //public ProductStatus? Status { get; set; }
    public decimal? Height { get; set; } // cm 
    public string? Material { get; set; }

    public ProductSaleType? ProductType { get; set; }
    // public string? Brand { get; set; }

    [JsonIgnore] public ProductStatus? ProductStatus { get; set; }
}

public class ProductUpdateImagesDto
{
    // Danh sách file ảnh mới, giữ đúng thứ tự index
    public List<IFormFile> Images { get; set; } = new();
}