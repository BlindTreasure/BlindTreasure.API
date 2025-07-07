using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class UpdateInventoryItemDto
{
    public int? Quantity { get; set; }

    public string? Location { get; set; }
    public InventoryItemStatus? Status { get; set; } // enum
}