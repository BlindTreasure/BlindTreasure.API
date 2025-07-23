using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class PromotionDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; } // "percentage" hoặc "fixed"
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? UsageLimit { get; set; }
    public PromotionStatus Status { get; set; } // "Approved", "PENDING", "Rejected"
    public Guid? SellerId { get; set; } // null nếu là global
    public string? RejectReason { get; set; }

    public RoleType? CreatedByRole { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool? IsParticipant { get; set; }
}