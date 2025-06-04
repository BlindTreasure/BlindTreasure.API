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

    public BlindBoxRarity Rarity { get; set; }
    public bool IsSecret => Rarity == BlindBoxRarity.Secret;

    public bool IsActive { get; set; }

    public ICollection<ProbabilityConfig> ProbabilityConfigs { get; set; }
}