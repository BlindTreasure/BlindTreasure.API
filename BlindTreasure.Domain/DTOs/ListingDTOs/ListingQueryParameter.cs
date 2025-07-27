using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ListingDTOs;

public class ListingQueryParameter : PaginationParameter
{
    public ListingStatus? Status { get; set; }
    public bool? IsFree { get; set; }
    public bool? IsOwnerListings { get; set; } // Filter để lấy listings của chính user hiện tại
    public Guid? UserId { get; set; } // Filter theo userId cụ thể
    public string? SearchByName { get; set; } // Filter theo tên sản phẩm
    public Guid? CategoryId { get; set; } // Filter theo category của sản phẩm
}