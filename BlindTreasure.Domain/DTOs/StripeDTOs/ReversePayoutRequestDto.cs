namespace BlindTreasure.Domain.DTOs.StripeDTOs;

public class ReversePayoutRequestDto
{
    public string TransferId { get; set; }
    public string? Reason { get; set; }
}