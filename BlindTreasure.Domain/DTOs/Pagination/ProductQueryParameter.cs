using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class ProductQueryParameter : PaginationParameter
{
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public ProductStatus? ProductStatus { get; set; }

    public Guid? SellerId { get; set; }


    public ProductSortField SortBy { get; set; } = ProductSortField.CreatedAt;

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTime? ReleaseDateFrom { get; set; }
    public DateTime? ReleaseDateTo { get; set; }
}

public enum ProductSortField
{
    CreatedAt,
    Name,
    Price,
    Stock
}