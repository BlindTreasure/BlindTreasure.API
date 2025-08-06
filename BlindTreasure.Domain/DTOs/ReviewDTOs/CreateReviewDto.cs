namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class CreateReviewDto
{
    public Guid OrderDetailId { get; set; }
    public int OverallRating { get; set; }
    public required string Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
}