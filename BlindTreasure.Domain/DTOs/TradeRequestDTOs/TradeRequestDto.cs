using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.TradeRequestDTOs;

public class TradeRequestDto
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public string? ListingItemName { get; set; }
    public Guid RequesterId { get; set; }
    public string? RequesterName { get; set; }
    public Guid? OfferedInventoryId { get; set; }
    public string? OfferedItemName { get; set; }
    public TradeRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public bool OwnerLocked { get; set; }
    public bool RequesterLocked { get; set; }
    public DateTime? LockedAt { get; set; }
}