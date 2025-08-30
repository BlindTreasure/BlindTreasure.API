using System.ComponentModel;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class UpdatePromotionDto
{

    [DefaultValue("mã giảm 100% cho đơn 1k")]
    public string? Description { get; set; } = "mã giảm 100% cho đơn 1k";

    [DefaultValue(DiscountType.Percentage)]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;


    [DefaultValue(100)] public decimal? DiscountValue { get; set; } = 100;

    [DefaultValue(typeof(DateTime), "2025-09-30T00:00:00Z")]
    public DateTime? StartDate { get; set; } 

    [DefaultValue(typeof(DateTime), "2025-09-30T00:00:00Z")]
    public DateTime? EndDate { get; set; } 
    [DefaultValue(100)] public int? UsageLimit { get; set; } = 100;

    [DefaultValue(2)] public int? MaxUsagePerUser { get; set; } = 2;
}