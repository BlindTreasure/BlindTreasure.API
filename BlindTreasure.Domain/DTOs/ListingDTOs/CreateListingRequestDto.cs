namespace BlindTreasure.Domain.DTOs.ListingDTOs;

public class CreateListingRequestDto
{
    public Guid InventoryId { get; set; }
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    public string Description { get; set; } // Thêm mô tả vào DTO
}