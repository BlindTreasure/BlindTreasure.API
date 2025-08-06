using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

public class TopSellingBlindBoxDto
{
    public Guid BlindBoxId { get; set; }
    public string BlindBoxName { get; set; } = string.Empty;
    public string BlindBoxImageUrl { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Price { get; set; }
}