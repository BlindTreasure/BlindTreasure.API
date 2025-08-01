﻿using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.TradeRequestDTOs;

public class TradeRequestDto
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public string? ListingItemName { get; set; }
    public RarityName? ListingItemTier { get; set; } // Đảm bảo trường này tồn tại
    public string? ListingItemTierDisplayName { get; set; } // Đảm bảo trường này tồn tại
    public Guid RequesterId { get; set; }
    public string? RequesterName { get; set; }
    public List<OfferedItemDto> OfferedItems { get; set; } = new();
    public string? OfferedItemName { get; set; }
    public TradeRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool OwnerLocked { get; set; }
    public bool RequesterLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public int TimeRemaining { get; set; } // Thời gian còn lại tính bằng giây

}

public class OfferedItemDto
{
    public Guid InventoryItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ImageUrl { get; set; }
    public RarityName? Tier { get; set; }
    public string? TierDisplayName { get; set; }
}