using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlindTreasure.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.ProductDTOs;

public class ProductCreateDto : ProductSellerCreateDto
{
    [Required] public Guid SellerId { get; set; } // Id của người bán
}

public class ProductSellerCreateDto
{
    [Required]
    [MaxLength(100)]
    [DefaultValue("Gundam v11")]
    public string Name { get; set; }

    [Required]
    [MaxLength(255)]
    [DefaultValue("Mô hình robot cao cấp, phiên bản giới hạn.")]
    public string Description { get; set; }

    [Required] public Guid CategoryId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    [DefaultValue(499000)]
    public decimal RealSellingPrice { get; set; } // Đây là giá bán thực tế, có thể khác với giá niêm yết

    [Range(0.01, double.MaxValue)]
    [DefaultValue(499000)]
    public decimal? ListedPrice { get; set; } // Đây là giá niêm yết, có thể khác với giá bán thực tế

    [Required]
    [Range(0, int.MaxValue)]
    [DefaultValue(10)]
    public int TotalStockQuantity { get; set; }

    [Required]
    [DefaultValue(ProductStatus.Active)]
    public ProductStatus Status { get; set; }

    [DefaultValue(15.5)] public decimal? Height { get; set; }

    [DefaultValue("Nhựa ABS cao cấp")] public string? Material { get; set; }

    [DefaultValue(ProductSaleType.DirectSale)]
    public ProductSaleType? ProductType { get; set; }

    // [DefaultValue("Bandai")] public string? Brand { get; set; }

    [MaxLength(6, ErrorMessage = "Tối đa 6 ảnh.")]
    public List<IFormFile>? Images { get; set; }
}