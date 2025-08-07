using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class ReviewQueryParameter : PaginationParameter
{
    public Guid? ProductId { get; set; }
    public Guid? BlindBoxId { get; set; }
    public Guid? SellerId { get; set; }
    public int? MinRating { get; set; }
    public int? MaxRating { get; set; }
    public bool? HasComment { get; set; }
    public bool? HasImages { get; set; }
    public string? SortBy { get; set; } // "rating_asc", "rating_desc", "newest", "oldest"
}