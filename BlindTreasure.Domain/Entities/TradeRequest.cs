using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class TradeRequest : BaseEntity
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public Guid RequesterId { get; set; } // User B
    public User? Requester { get; set; }
    public List<TradeRequestItem> OfferedItems { get; set; } = new();
    public InventoryItem? OfferedInventory { get; set; }
    public TradeRequestStatus Status { get; set; } = TradeRequestStatus.PENDING;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public bool OwnerLocked { get; set; } // User A lock
    public bool RequesterLocked { get; set; } // User B lock
    public DateTime? LockedAt { get; set; } // Thời điểm đủ lock (cả 2)
    public int TimeRemaining { get; set; } // Thời gian còn lại tính bằng giây
}