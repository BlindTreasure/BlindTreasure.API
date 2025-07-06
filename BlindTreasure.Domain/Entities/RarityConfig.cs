using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class RarityConfig : BaseEntity
{
    public RarityName Name { get; set; }
    public int Weight { get; set; }
    public bool IsSecret { get; set; }
    public Guid BlindBoxItemId { get; set; }
    public BlindBoxItem? BlindBoxItem { get; set; }
}