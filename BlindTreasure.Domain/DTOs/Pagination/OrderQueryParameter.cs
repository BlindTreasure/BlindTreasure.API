using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class OrderQueryParameter : PaginationParameter
{
    /// <summary>
    ///     Lọc theo trạng thái đơn hàng (PENDING, PAID, COMPLETED, CANCELLED, FAILED, EXPIRED)
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
}