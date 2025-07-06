namespace BlindTreasure.Domain.Entities;

public class PromotionParticipant : BaseEntity
{
    public Guid PromotionId { get; set; }
    public Promotion Promotion { get; set; }

    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public DateTime JoinedAt { get; set; }
}
