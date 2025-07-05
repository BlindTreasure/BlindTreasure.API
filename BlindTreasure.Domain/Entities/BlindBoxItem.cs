using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class BlindBoxItem : BaseEntity
{
    public Guid BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public int Quantity { get; set; }
    public decimal DropRate { get; set; }

    public Guid RarityId { get; set; }
    public RarityConfig Rarity { get; set; }
    public bool IsSecret { get; set; }

    public bool IsActive { get; set; }

    public ICollection<ProbabilityConfig> ProbabilityConfigs { get; set; }
}