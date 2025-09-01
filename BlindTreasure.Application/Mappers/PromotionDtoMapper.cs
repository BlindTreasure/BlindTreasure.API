using BlindTreasure.Domain.DTOs.PromotionDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Mappers;

public static class PromotionDtoMapper
{
    public static PromotionDto ToPromotionDto(Promotion promotion)
    {
        if (promotion == null) return null;
        return new PromotionDto
        {
            Id = promotion.Id,
            Code = promotion.Code,
            Description = promotion.Description,
            DiscountType = promotion.DiscountType,
            DiscountValue = promotion.DiscountValue,
            StartDate = promotion.StartDate,
            EndDate = promotion.EndDate,
            UsageLimit = promotion.UsageLimit,
            Status = promotion.Status,
            SellerId = promotion.SellerId,
            RejectReason = promotion.RejectReason,
            CreatedByRole = promotion.CreatedByRole,
            UpdatedAt = promotion.UpdatedAt,
            IsDeleted = promotion.IsDeleted,
            MaxUsagePerUser = promotion.MaxUsagePerUser,
            PromotionUserUsages = promotion.PromotionUserUsages?.Select(ToPromotionUsageDto).ToList() ?? new List<PromotionUserUsageDto>()
        };
    }

    public static PromotionUserUsageDto ToPromotionUsageDto(PromotionUserUsage promotionUserUsage)
    {
        if (promotionUserUsage == null) return null;
        return new PromotionUserUsageDto
        {
            Id = promotionUserUsage.Id,
            PromotionId = promotionUserUsage.PromotionId,
            UserId = promotionUserUsage.UserId,
            User = promotionUserUsage.User != null ? UserMapper.ToUserDto(promotionUserUsage.User) : null,
            UsageCount = promotionUserUsage.UsageCount,
            LastUsedAt = promotionUserUsage.LastUsedAt,
            IsMaxUsageReached = promotionUserUsage.IsMaxUsageReached,
            Promotion = promotionUserUsage.Promotion != null ? ToPromotionDto(promotionUserUsage.Promotion) : null
        };
    }
}