namespace BlindTreasure.Domain.DTOs.ListingDTOs;

public class CreateListingRequestDto
{
    public Guid InventoryId { get; set; }
    public bool IsFree { get; set; }
    public Guid? DesiredItemId { get; set; }
    public string? DesiredItemName { get; set; }
}