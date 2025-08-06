using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

public class SellerStatisticsResponseDto
{
    public string Range { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<int> Sales { get; set; } = new(); // Số lượng đơn hàng
    public List<decimal> Revenue { get; set; } = new(); // Doanh thu
}