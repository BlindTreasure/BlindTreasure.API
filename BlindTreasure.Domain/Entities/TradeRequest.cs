using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class TradeRequest : BaseEntity
{
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; }

    public Guid RequesterId { get; set; } // User gửi yêu cầu
    public User Requester { get; set; }

    public Guid? OfferedInventoryId { get; set; } // Item mà requester đề xuất đổi
    public InventoryItem? OfferedInventory { get; set; }

    public TradeRequestStatus Status { get; set; } = TradeRequestStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime? LockedAt { get; set; }
}