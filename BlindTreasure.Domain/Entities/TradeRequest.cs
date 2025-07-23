using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class TradeRequest : BaseEntity
{
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; }

    public Guid RequesterId { get; set; } // User B
    public User Requester { get; set; }

    public Guid? OfferedInventoryId { get; set; } // Item mà requester đề xuất đổi
    public InventoryItem? OfferedInventory { get; set; }

    public TradeRequestStatus Status { get; set; } = TradeRequestStatus.PENDING;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    public bool OwnerLocked { get; set; } = false; // User A lock
    public bool RequesterLocked { get; set; } = false; // User B lock

    public DateTime? LockedAt { get; set; } // Thời điểm đủ lock (cả 2)
}