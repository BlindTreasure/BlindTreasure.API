namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class CreateReviewDto
{
    public Guid OrderDetailId { get; set; }
    public int OverallRating { get; set; }
    public int? QualityRating { get; set; }
    public int? ServiceRating { get; set; }
    public int? DeliveryRating { get; set; }
    public string Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
}