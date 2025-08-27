using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerDTOs;

public class SellerOverviewDto
{
    public Guid SellerId { get; set; }
    public double AverageRating { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? JoinedAtToText { get; set; }
    public int ProductCount { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyArea { get; set; }

    public int ProductInSellingCount { get; set; }
    public int ProductInBlindBoxCount { get; set; }
    public int BlindBoxCount { get; set; }
}