using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

/// <summary>
/// Tham số truy vấn cho danh sách khuyến mãi
/// </summary>
public class PromotionQueryParameter : PaginationParameter 
{
    /// <summary>
    /// Lọc theo trạng thái voucher
    /// ### Giá trị hợp lệ:
    /// - PENDING: Đang chờ duyệt
    /// - Approved: Đã được duyệt
    /// - Rejected: Đã bị từ chối
    /// </summary>
    public PromotionStatus? Status { get; set; }

    /// <summary>
    /// Lọc theo ID của seller (người bán)
    /// - Chỉ áp dụng cho voucher riêng của seller
    /// - Để trống nếu muốn lấy tất cả
    /// </summary>
    public Guid? SellerId { get; set; }

    /// <summary>
    /// Lọc theo loại voucher
    /// - true: Chỉ lấy voucher toàn sàn (global)
    /// - false: Chỉ lấy voucher riêng của seller
    /// - null: Lấy tất cả loại voucher
    /// </summary>
    public bool? IsGlobal { get; set; }
}
