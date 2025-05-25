namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class SellerRegistrationDto
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }

    public required string CompanyName { get; set; }
    public required string TaxId { get; set; }
    public required string CompanyAddress { get; set; }
    public required string CoaDocumentUrl { get; set; }
}