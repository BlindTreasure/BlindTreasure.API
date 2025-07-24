namespace BlindTreasure.Domain.Entities;

public class TradeRequestItem : BaseEntity
{
    public Guid TradeRequestId { get; set; }
    public TradeRequest? TradeRequest { get; set; }

    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}