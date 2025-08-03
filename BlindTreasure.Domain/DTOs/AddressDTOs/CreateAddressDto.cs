using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.AddressDTOs;

public class CreateAddressDto
{
    [DefaultValue("Quang wibu")] public string FullName { get; set; }

    [DefaultValue("0987570351")] public string Phone { get; set; }

    [DefaultValue("Bưng Ông Thoàn, Phường Phú Hữu, TP.Thủ Đức, HCM")]
    public string AddressLine { get; set; }

    [DefaultValue("Thành Phố Thủ Đức")] public string City { get; set; }

    [DefaultValue("Hồ Chí Minh")] public string Province { get; set; }
    [DefaultValue("Phường Phú Hữu")] public string? Ward { get; set; }
    [DefaultValue("Thành Phố Thủ Đức")] public string? District { get; set; }

    public string? PostalCode { get; set; } = "90763"; // Default value for PostalCode HCM CITY
    public bool IsDefault { get; set; } = false; // Default value for IsDefault
}