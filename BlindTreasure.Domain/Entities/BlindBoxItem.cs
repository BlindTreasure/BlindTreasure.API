namespace BlindTreasure.Domain.Entities;

using System.ComponentModel.DataAnnotations;

public class BlindBoxItem : BaseEntity
{
    public Guid BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public bool IsSecret { get; set; }
    public int DisplayOrder { get; set; }

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public ICollection<ProbabilityConfig> ProbabilityConfigs { get; set; } = new List<ProbabilityConfig>();
}
