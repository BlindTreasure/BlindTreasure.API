namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class PromotionApplicationResultDto
{
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string PromotionCode { get; set; }
    public string Message { get; set; }
}