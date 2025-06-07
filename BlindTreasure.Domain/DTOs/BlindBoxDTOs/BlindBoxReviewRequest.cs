namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class BlindBoxReviewRequest
{
    public bool Approve { get; set; }
    public string? RejectReason { get; set; }
}