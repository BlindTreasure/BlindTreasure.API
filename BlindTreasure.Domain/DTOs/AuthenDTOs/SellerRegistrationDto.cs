using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class SellerRegistrationDto
{
    [DefaultValue("trangiaphuc362003181@gmail.com")]
    public required string Email { get; set; }

    [DefaultValue("trangiaphuc362003181")] public required string Password { get; set; }

    [DefaultValue("trangiaphuc362003181")] public required string FullName { get; set; }

    [DefaultValue("090909090")] public string? PhoneNumber { get; set; }

    [DefaultValue("2003-03-06T00:00:00Z")] public DateTime DateOfBirth { get; set; }

    [DefaultValue("Binna")] public required string CompanyName { get; set; }

    [DefaultValue("030303030")] public required string TaxId { get; set; }

    [DefaultValue("khu cong nghe cao")] public required string CompanyAddress { get; set; }
}