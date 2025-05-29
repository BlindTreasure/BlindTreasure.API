namespace BlindTreasure.Domain.DTOs.SellerDTOs;

public class UpdateSellerInfoDto
{
    public required string FullName { get; set; }
    public required string PhoneNumber { get; set; }
    public required DateTime DateOfBirth { get; set; }
    public required string CompanyName { get; set; }
    public required string TaxId { get; set; }
    public required string CompanyAddress { get; set; }
}