namespace BlindTreasure.Domain.DTOs.Pagination;

public class SellerParticipantPromotionParameter : PaginationParameter
{
    public Guid? PromotionId { get; set; }
    public Guid? SellerId { get; set; }
}