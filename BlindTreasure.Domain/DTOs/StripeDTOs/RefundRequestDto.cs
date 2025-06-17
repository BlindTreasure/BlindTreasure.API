namespace BlindTreasure.Domain.DTOs.StripeDTOs;

public class RefundRequestDto
{
    public string PaymentIntentId { get; set; }
    public decimal Amount { get; set; }
    public decimal? Reason { get; set; }
}