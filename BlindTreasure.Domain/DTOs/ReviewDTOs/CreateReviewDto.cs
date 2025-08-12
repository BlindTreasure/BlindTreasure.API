using System.ComponentModel;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class CreateReviewDto
{
    public Guid OrderDetailId { get; set; }
    [DefaultValue(3)] public int Rating { get; set; }

    [DefaultValue("sản phẩm chất lượng cao")]
    public string Comment { get; set; } = string.Empty;

    public List<IFormFile>? Images { get; set; } = new();
}