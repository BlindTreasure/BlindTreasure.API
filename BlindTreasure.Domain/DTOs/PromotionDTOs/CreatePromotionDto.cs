using System.ComponentModel;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class CreatePromotionDto
{
    [DefaultValue("")] public string Code { get; set; } = string.Empty;

    [DefaultValue("mã giảm 100% cho đơn 1k")]
    public string Description { get; set; } = string.Empty;

    [DefaultValue(DiscountType.Percentage)]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [DefaultValue(100)] public decimal DiscountValue { get; set; } = 0;

    [DefaultValue(typeof(DateTime), "2000-01-01T00:00:00Z")]
    public DateTime StartDate { get; set; } =
        DateTime.SpecifyKind(DateTime.Parse("2000-01-01T00:00:00Z"), DateTimeKind.Utc);

    [DefaultValue(typeof(DateTime), "2100-01-01T00:00:00Z")]
    public DateTime EndDate { get; set; } =
        DateTime.SpecifyKind(DateTime.Parse("2100-01-01T00:00:00Z"), DateTimeKind.Utc);

    [DefaultValue(100)] public int UsageLimit { get; set; }
    [DefaultValue(2)] public int? MaxUsagePerUser { get; set; } = 2; // e.g. 2
}