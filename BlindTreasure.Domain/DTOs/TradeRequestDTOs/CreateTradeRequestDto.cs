namespace BlindTreasure.Domain.DTOs.TradeRequestDTOs;

public class CreateTradeRequestDto
{
    public Guid ListingId { get; set; }
    public Guid OfferedInventoryId { get; set; }
}
