using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PromotionDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IPromotionService
{
    Task<Pagination<PromotionDto>> GetPromotionsAsync(PromotionQueryParameter param);
    Task<PromotionDto> GetPromotionByIdAsync(Guid id);
    Task<PromotionDto> CreatePromotionAsync(CreatePromotionDto dto);
    Task<PromotionDto> DeletePromotionAsync(Guid id);
    Task<PromotionDto> UpdatePromotionAsync(Guid id, UpdatePromotionDto dto);
    Task<PromotionDto> ReviewPromotionAsync(ReviewPromotionDto dto);
    Task<PromotionApplicationResultDto> ApplyVoucherAsync(string voucherCode, Guid orderId);
    Task<ParticipantPromotionDto> ParticipatePromotionAsync(Guid id);
    Task<ParticipantPromotionDto> WithdrawPromotionAsync(WithdrawParticipantPromotionDto dto);
    Task<List<SellerParticipantDto>> GetPromotionParticipantsAsync(SellerParticipantPromotionParameter param);
    Task<List<PromotionUserUsageDto>> GetPromotionUsageOfUserAsync(Guid userId);
    Task<PromotionUserUsageDto> GetSpecificPromotionUsagesync(Guid promotionId, Guid userId);
}