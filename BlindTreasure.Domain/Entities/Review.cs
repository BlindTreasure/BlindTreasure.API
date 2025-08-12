namespace BlindTreasure.Domain.Entities;

public class Review : BaseEntity
{
    // Thông tin cơ bản
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public Guid? OrderDetailId { get; set; }
    public OrderDetail? OrderDetail { get; set; }

    // Sản phẩm được review
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? BlindBoxId { get; set; }
    public BlindBox? BlindBox { get; set; }

    // Thông tin seller - THÊM NULLABLE
    public Guid? SellerId { get; set; } // Thay đổi từ Guid thành Guid?
    public Seller? Seller { get; set; }

    // Đánh giá
    public int OverallRating { get; set; } // 1-5 sao

    // Add these new properties
    public string? Content { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public List<string> ImageUrls { get; set; } = new();

    // Phản hồi từ seller
    public string? SellerResponse { get; set; }
    public DateTime? SellerResponseDate { get; set; }
}