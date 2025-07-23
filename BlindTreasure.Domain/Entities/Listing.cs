using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Listing : BaseEntity
{
    public Guid InventoryId { get; set; }
    public InventoryItem InventoryItem { get; set; }
    public bool IsFree { get; set; } = false;
    public string? Description { get; set; } // Mô tả listing
    public DateTime ListedAt { get; set; }
    public ListingStatus Status { get; set; }
    public TradeStatus TradeStatus { get; set; }
}