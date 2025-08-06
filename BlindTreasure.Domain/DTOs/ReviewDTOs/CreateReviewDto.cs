using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class CreateReviewDto
{
    [Required(ErrorMessage = "Order detail ID is required.")]
    public Guid OrderDetailId { get; set; } // ID của OrderDetail mà khách hàng muốn review

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Comment is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Comment must be between 10 and 2000 characters.")]
    public string Comment { get; set; } = string.Empty;

    public List<string> ImagesUrls { get; set; } = new();
}
