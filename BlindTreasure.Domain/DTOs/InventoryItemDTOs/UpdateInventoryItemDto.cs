using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class UpdateInventoryItemDto
{

    public string? Location { get; set; }
    public InventoryItemStatus? Status { get; set; } // enum
}