using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class SellerRegistrationDto
{
    [DefaultValue("trangiaphuc362003181@gmail.com")]
    public required string Email { get; set; }
    [DefaultValue("trangiaphuc362003181@gmail.com")]
    public required string Password { get; set; }
    [DefaultValue("trangiaphuc362003181@gmail.com")]
    public required string FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }

    public required string CompanyName { get; set; }
    public required string TaxId { get; set; }
    public required string CompanyAddress { get; set; }
    public required string CoaDocumentUrl { get; set; }
}