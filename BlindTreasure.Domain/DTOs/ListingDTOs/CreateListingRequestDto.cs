namespace BlindTreasure.Domain.DTOs.ListingDTOs;

public class CreateListingRequestDto
{
    public Guid InventoryId { get; set; }
    public decimal Price { get; set; }
}