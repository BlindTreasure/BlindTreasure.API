using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.SellerDTOs;

public class SellerDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public DateTime DateOfBirth { get; set; }

    public string? CompanyName { get; set; } = "BlindTreasure-Collaboration"; // FromName
    public string? CompanyPhone { get; set; } = "0987654321"; // FromPhone

    public string? CompanyAddress { get; set; } =
        "72 Thành Thái, Phường 14, Quận 10, Hồ Chí Minh, Vietnam"; // FromAddress

    public string? CompanyWardName { get; set; } = "Phường 14"; // FromWardName
    public string? CompanyDistrictName { get; set; } = "Quận 10"; // FromDistrictName
    public string? CompanyProvinceName { get; set; } = "HCM"; // FromProvinceName


    public string? CoaDocumentUrl { get; set; }
    public SellerStatus Status { get; set; }
    public bool IsVerified { get; set; }
    public string? StripeAccountId { get; set; }
    public string? TaxId { get; set; } // Mã số thuế
}