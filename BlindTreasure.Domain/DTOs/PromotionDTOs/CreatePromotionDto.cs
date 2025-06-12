using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class CreatePromotionDto
{
    public string Code { get; set; }
    public string Description { get; set; }
    public DiscountType DiscountType { get; set; } // "percentage" | "fixed"
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UsageLimit { get; set; }
}