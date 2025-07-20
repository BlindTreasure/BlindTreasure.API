namespace BlindTreasure.Domain.Entities;

public class ListingReport : BaseEntity
{
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; }

    public string Reason { get; set; }
    public DateTime ReportedAt { get; set; }
}