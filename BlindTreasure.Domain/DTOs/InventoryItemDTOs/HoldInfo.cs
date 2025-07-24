namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;
public class HoldInfoDto
{
    public bool IsOnHold { get; set; }
    public DateTime? HoldUntil { get; set; }
    public double? RemainingDays { get; set; }
    public string? LastTradeId { get; set; }
}

