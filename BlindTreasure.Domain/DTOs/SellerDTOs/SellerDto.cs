using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.SellerDTOs;

public class SellerDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public DateTime DateOfBirth { get; set; }

    public string? CompanyName { get; set; }
    public string? TaxId { get; set; }
    public string? CompanyAddress { get; set; }

    public string? CoaDocumentUrl { get; set; }
    public SellerStatus Status { get; set; }
    public bool IsVerified { get; set; }
    public string? StripeAccountId { get; set; }
}