namespace BlindTreasure.Domain.DTOs.SellerDTOs;

public class SellerProfileDto
{
    public Guid SellerId { get; set; }
    public Guid UserId { get; set; }

    // User info
    public string? FullName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; }

    // Seller info
    public string? CompanyName { get; set; }
    public string? TaxId { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CoaDocumentUrl { get; set; }
    public string SellerStatus { get; set; }
    public bool IsVerified { get; set; }
    public string? RejectReason { get; set; }
}