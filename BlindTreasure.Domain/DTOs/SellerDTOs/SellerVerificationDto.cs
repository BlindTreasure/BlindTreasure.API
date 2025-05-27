namespace BlindTreasure.Domain.DTOs.SellerDTOs;

public class SellerVerificationDto
{
    public bool IsApproved { get; set; }

    public string? RejectReason { get; set; }
}