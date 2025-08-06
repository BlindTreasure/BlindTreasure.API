using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

/// <summary>
/// Tham số truy vấn cho danh sách khuyến mãi
/// </summary>
public class PromotionQueryParameter : PaginationParameter
{
    public PromotionStatus? Status { get; set; }
    public Guid? SellerId { get; set; }
    public bool? IsGlobal { get; set; }
    public bool? IsParticipated { get; set; } // True: Lấy promotions mà seller tham gia
    public Guid? ParticipantSellerId { get; set; } // SellerId để check participation
}