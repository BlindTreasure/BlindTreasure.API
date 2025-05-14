namespace BlindTreasure.Domain.Entities;

public class ProbabilityConfig : BaseEntity
{
    // FK → BlindBoxItem
    public Guid BlindBoxItemId { get; set; }
    public BlindBoxItem BlindBoxItem { get; set; }

    public decimal Probability { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }

    // FK → User (nhân viên phê duyệt)
    public Guid ApprovedBy { get; set; }
    public User ApprovedByUser { get; set; }

    public DateTime ApprovedAt { get; set; }
}