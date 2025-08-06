using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class ReviewQueryParameter : PaginationParameter
{
    public Guid? ProductId { get; set; }
    public Guid? BlindBoxId { get; set; }
    public Guid? SellerId { get; set; }
    public Guid? UserId { get; set; } // Thêm nếu cần filter theo user
    public bool? IsPublished { get; set; } = true; // Mặc định chỉ lấy review đã publish
    public ReviewStatus? Status { get; set; } // Filter theo status nếu cần
    public int? MinRating { get; set; } // Filter theo rating tối thiểu
    public bool? IsVerifiedPurchase { get; set; } // Chỉ lấy review từ người đã mua
}