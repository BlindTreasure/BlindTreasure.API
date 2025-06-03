namespace BlindTreasure.Domain.DTOs.SellerDTOs;

public class UpdateSellerInfoDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? CompanyName { get; set; }
    public string? TaxId { get; set; }
    public string? CompanyAddress { get; set; }
}