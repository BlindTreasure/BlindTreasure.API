using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

public class SellerStatisticsDto
{
    // Tổng đơn
    public int TotalOrders { get; set; }

    // Tổng sản phẩm đã bán
    public int TotalItemsSold { get; set; }

    // Doanh thu gộp (trước giảm giá)
    public decimal GrossRevenue { get; set; }

    // Tổng giảm giá seller áp dụng
    public decimal TotalDiscount { get; set; }

    // Doanh thu ròng
    public decimal NetRevenue { get; set; }

    // Giá trị trung bình mỗi đơn (AOV)
    public decimal AverageOrderValue { get; set; }

    // Tỉ lệ refund (nếu cần)
    public decimal RefundRate { get; set; }

    // Tổng phí vận chuyển seller đã thu
    public decimal ShippingFees { get; set; }

    // Phân trang (tuỳ chọn)
    public int Page { get; set; }
    public int PageSize { get; set; }
}