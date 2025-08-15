using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.TradeHistoryDTOs;

public class TradeHistoryDto
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public string ListingItemName { get; set; }
    public string ListingItemImage { get; set; }
    public Guid? ListingInventoryItemId { get; set; }
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; }
    public Guid? OfferedInventoryId { get; set; }
    public string OfferedItemName { get; set; }
    public string OfferedItemImage { get; set; }
    public TradeRequestStatus FinalStatus { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}