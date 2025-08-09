namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class ReviewResponseDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ItemName { get; set; } // Tên sản phẩm/blindbox
    public List<string> Images { get; set; } = new();
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public SellerReplyDto? SellerReply { get; set; }

    // Metadata
    public Guid? OrderDetailId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? BlindBoxId { get; set; }
    public Guid? SellerId { get; set; }
}

public class SellerReplyDto
{
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? SellerName { get; set; }
}