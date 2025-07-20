using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Listing : BaseEntity
{
    public Guid InventoryId { get; set; }
    public InventoryItem InventoryItem { get; set; }

    public bool IsFree { get; set; } = false;

    public Guid? DesiredItemId { get; set; }
    public string? DesiredItemName { get; set; }

    public string? Description { get; set; } // Mô tả listing

    public DateTime ListedAt { get; set; }
    public ListingStatus Status { get; set; }
    public TradeStatus TradeStatus { get; set; }
}