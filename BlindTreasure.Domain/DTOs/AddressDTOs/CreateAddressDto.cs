using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.AddressDTOs;

public class CreateAddressDto
{
    [DefaultValue("Quang wibu")] public string FullName { get; set; }

    [DefaultValue("0123456789")] public string Phone { get; set; }

    [DefaultValue("72 Thành Thái, Phường 14, Quận 10, Hồ Chí Minh, Vietnam")]
    public string AddressLine { get; set; }

    [DefaultValue("HCM")] public string City { get; set; }

    [DefaultValue("Hồ Chí Minh")] public string Province { get; set; }
    [DefaultValue("Phường 14")] public string? Ward { get; set; }
    [DefaultValue(" Quận 10")] public string? District { get; set; }

    public string? PostalCode { get; set; } = "700000"; // Default value for PostalCode HCM CITY
    public bool IsDefault { get; set; } = false; // Default value for IsDefault
}