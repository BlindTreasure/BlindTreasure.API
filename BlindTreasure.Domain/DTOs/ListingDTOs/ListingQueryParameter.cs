using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ListingDTOs;

public class ListingQueryParameter : PaginationParameter
{
    public ListingStatus? Status { get; set; }
    public bool? IsFree { get; set; } // Filter theo IsFree
}

