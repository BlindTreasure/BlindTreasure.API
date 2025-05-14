namespace BlindTreasure.Domain.Entities;

public class BlindBoxItem : BaseEntity
{
    // FK → BlindBox
    public Guid BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    // FK → Product
    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public int Quantity { get; set; }
    public decimal DropRate { get; set; }
    public bool IsSecret { get; set; }
    public string Rarity { get; set; }
    public decimal Weight { get; set; }
    public string Sku { get; set; }
    public bool IsActive { get; set; }

    // 1-n → ProbabilityConfigs
    public ICollection<ProbabilityConfig> ProbabilityConfigs { get; set; }
}