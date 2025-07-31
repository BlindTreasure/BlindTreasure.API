namespace BlindTreasure.Domain.DTOs.TradeRequestDTOs;

public class CreateTradeRequestDto
{
    public List<Guid> OfferedInventoryIds { get; set; } = new(); // Multiple items
}