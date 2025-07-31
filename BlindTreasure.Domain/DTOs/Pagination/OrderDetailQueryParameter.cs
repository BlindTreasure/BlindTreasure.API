using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class OrderDetailQueryParameter : PaginationParameter
{
    /// <summary>
    /// Lọc theo trạng thái order detail
    /// </summary>
    public OrderDetailItemStatus? Status { get; set; }

    /// <summary>
    /// Lọc theo OrderId
    /// </summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Lọc theo giá tối thiểu
    /// </summary>
    public decimal? MinPrice { get; set; }

    /// <summary>
    /// Lọc theo giá tối đa
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Chỉ lấy order detail là BlindBox
    /// </summary>
    public bool? IsBlindBox { get; set; }

    /// <summary>
    /// Chỉ lấy order detail là Product
    /// </summary>
    public bool? IsProduct { get; set; }
}