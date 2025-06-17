namespace BlindTreasure.Domain.DTOs.AddressDTOs;

public class AddressDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public string City { get; set; }
    public string Province { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
    public bool IsDefault { get; set; }
}