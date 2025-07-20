using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class TradeHistory : BaseEntity
{
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; }

    public Guid RequesterId { get; set; }
    public User Requester { get; set; }

    public Guid? OfferedInventoryId { get; set; }
    public InventoryItem? OfferedInventory { get; set; }

    public TradeRequestStatus FinalStatus { get; set; }   // Accepted/Rejected/Cancelled
    public DateTime CompletedAt { get; set; }
}