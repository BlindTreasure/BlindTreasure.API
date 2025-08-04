using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.TradeRequestDTOs;

public class TradeRequestDto
{
    // Thông tin cơ bản của yêu cầu trade
    public Guid Id { get; set; }
    public TradeRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public int TimeRemaining { get; set; }

    // Thông tin về listing (item được yêu cầu trade)
    public Guid ListingId { get; set; }
    public string? ListingItemName { get; set; }
    public RarityName? ListingItemTier { get; set; }
    public string? ListingItemImgUrl { get; set; }

    // Thông tin chủ sở hữu listing
    public string? ListingOwnerName { get; set; }
    public string? ListingOwnerAvatarUrl { get; set; }

    // Thông tin người yêu cầu trade
    public Guid RequesterId { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterAvatarUrl { get; set; }

    // Thông tin về các item được offer
    public List<OfferedItemDto> OfferedItems { get; set; } = new();

    // Thông tin lock
    public bool OwnerLocked { get; set; }
    public bool RequesterLocked { get; set; }
    public DateTime? LockedAt { get; set; }
}

public class OfferedItemDto
{
    public Guid InventoryItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ImageUrl { get; set; }
    public RarityName? Tier { get; set; }
    public string? TierDisplayName { get; set; }
}