using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UnboxLogDTOs;

public class UnboxLogDto
{
    public Guid Id { get; set; }
    public Guid CustomerBlindBoxId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public RarityName Rarity { get; set; }
    public decimal DropRate { get; set; }
    public decimal RollValue { get; set; }
    public DateTime UnboxedAt { get; set; }
    public string BlindBoxName { get; set; }
    public string Reason { get; set; }
}