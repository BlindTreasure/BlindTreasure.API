using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class BlindBoxQueryParameter : PaginationParameter
{
    public string? Search { get; set; }
    public Guid? SellerId { get; set; }
    public BlindBoxStatus? Status { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTime? ReleaseDateFrom { get; set; }
    public DateTime? ReleaseDateTo { get; set; }

    public bool? HasItem { get; set; }
}