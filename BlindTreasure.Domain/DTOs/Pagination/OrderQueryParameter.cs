using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class OrderQueryParameter : PaginationParameter
{
    /// <summary>
    ///     Lọc theo trạng thái đơn hàng (PENDING, PAID, COMPLETED, CANCELLED, EXPIRED)
    /// </summary>
    public OrderStatus? Status { get; set; }

    /// <summary>
    ///     Lọc theo ngày đặt hàng từ...
    /// </summary>
    public DateTime? PlacedFrom { get; set; }

    /// <summary>
    ///     Lọc theo ngày đặt hàng đến...
    /// </summary>
    public DateTime? PlacedTo { get; set; }

    public Guid? CheckoutGroupId { get; set; } = Guid.Empty;
}