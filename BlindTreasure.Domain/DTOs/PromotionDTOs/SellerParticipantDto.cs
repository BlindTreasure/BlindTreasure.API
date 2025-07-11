namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class SellerParticipantDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }

    public string? CompanyName { get; set; }
    public string? TaxId { get; set; }
    public string? CompanyAddress { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? JoinedAt { get; set; }
}