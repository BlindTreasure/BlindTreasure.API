namespace BlindTreasure.Domain.Entities;

public class BlindBoxItem : BaseEntity
{
    public Guid BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public bool IsSecret { get; set; }
    public int DisplayOrder { get; set; }
    public string ImageUrl { get; set; }
    public string Description { get; set; }

    public ICollection<ProbabilityConfig> ProbabilityConfigs { get; set; }
}