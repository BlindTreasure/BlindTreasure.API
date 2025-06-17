namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class ReviewPromotionDto
{
    public Guid PromotionId { get; set; }
    public bool IsApproved { get; set; }
    public string? RejectReason { get; set; }
}