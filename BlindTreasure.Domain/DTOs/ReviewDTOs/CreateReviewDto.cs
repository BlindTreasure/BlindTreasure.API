using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class CreateReviewDto
{
    public Guid OrderDetailId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public List<IFormFile>? Images { get; set; } = new();
}