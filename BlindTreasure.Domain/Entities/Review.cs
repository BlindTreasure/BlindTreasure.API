using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Review : BaseEntity
{
    // Thông tin cơ bản
    public Guid UserId { get; set; }
    public User User { get; set; }

    // Liên kết với đơn hàng (BẮT BUỘC để validate đã mua)
    public Guid OrderDetailId { get; set; }
    public OrderDetail OrderDetail { get; set; }

    // Sản phẩm được review
    public Guid? ProductId { get; set; }
    public Product Product { get; set; }
    public Guid? BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    // Thông tin seller
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    // Đánh giá
    public int OverallRating { get; set; } // 1-5 sao
    public int? QualityRating { get; set; } // Chất lượng sản phẩm
    public int? ServiceRating { get; set; } // Dịch vụ bán hàng
    public int? DeliveryRating { get; set; } // Giao hàng

    // Nội dung review (AI validation)
    public string OriginalComment { get; set; } // Comment gốc từ user
    public string? ProcessedComment { get; set; } // Comment sau khi AI xử lý/filter
    public bool IsCommentValid { get; set; } // AI đánh giá comment có hợp lệ không
    public string? ValidationReason { get; set; } // Lý do AI từ chối/cảnh báo

    // Hình ảnh
    public List<string> ImageUrls { get; set; } = new();

    // Trạng thái
    public ReviewStatus Status { get; set; } = ReviewStatus.PendingValidation;
    public bool IsPublished { get; set; } = false;
    public bool IsVerifiedPurchase { get; set; } = true;

    // Phản hồi từ seller
    public string? SellerResponse { get; set; }
    public DateTime? SellerResponseDate { get; set; }

    // AI Metadata
    public DateTime? AiValidatedAt { get; set; }
    public string? AiValidationDetails { get; set; } // JSON chi tiết từ AI
}