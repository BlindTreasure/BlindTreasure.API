namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class UpdateInventoryItemDto
{
    public int? Quantity { get; set; }

    public string? Location { get; set; }
    public string? Status { get; set; }
}