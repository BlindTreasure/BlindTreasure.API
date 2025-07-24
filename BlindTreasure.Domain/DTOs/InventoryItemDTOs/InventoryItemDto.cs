using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class InventoryItemDto
{
    public Guid InventoryItemId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public ProducDetailDto? Product { get; set; }

    public int Quantity { get; set; }
    public string Location { get; set; }
    public InventoryItemStatus Status { get; set; } // enum
    public DateTime CreatedAt { get; set; }

    public bool IsFromBlindBox { get; set; }
    public Guid? SourceCustomerBlindBoxId { get; set; }
    public HoldInfoDto? HoldInfo { get; set; }
}