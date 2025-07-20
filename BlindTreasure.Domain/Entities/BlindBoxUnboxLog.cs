using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class BlindBoxUnboxLog : BaseEntity
{
    public Guid CustomerBlindBoxId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public RarityName Rarity { get; set; }
    public decimal DropRate { get; set; }
    public decimal RollValue { get; set; }
    public string ProbabilityTableJson { get; set; }
    public DateTime UnboxedAt { get; set; }
    public string BlindBoxName { get; set; }
    public string Reason { get; set; } = string.Empty;
}