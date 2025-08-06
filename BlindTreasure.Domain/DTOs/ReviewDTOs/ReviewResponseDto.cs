namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class ReviewResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } // Sử dụng DateTime, khi gửi response ra client sẽ tự format
    public string? Category { get; set; } // Tên danh mục sản phẩm (nếu review sản phẩm)
    public List<string>? Images { get; set; } // Danh sách public URLs của ảnh
    public SellerReplyDto? SellerReply { get; set; }
    public bool IsApproved { get; set; } // Trạng thái duyệt của review
}

public class SellerReplyDto
{
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } // Sử dụng DateTime
}