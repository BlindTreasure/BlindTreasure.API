using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

public class SellerStatisticsRequestDto
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    // Cho tương lai nếu cần phân trang, lọc theo sản phẩm, category…
    public Guid? ProductId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}