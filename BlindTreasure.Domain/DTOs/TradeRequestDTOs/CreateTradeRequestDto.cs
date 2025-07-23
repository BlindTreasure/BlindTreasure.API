namespace BlindTreasure.Domain.DTOs.TradeRequestDTOs;

public class CreateTradeRequestDto
{
    public Guid ListingId { get; set; }
    public List<Guid> OfferedInventoryIds { get; set; } = new(); // Multiple items
}