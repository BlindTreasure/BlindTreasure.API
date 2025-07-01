using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PromotionDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IPromotionService
{
    Task<Pagination<PromotionDto>> GetPromotionsAsync(PromotionQueryParameter param);
    Task<PromotionDto> CreatePromotionAsync(CreatePromotionDto dto);
    Task<PromotionDto> DeletePromotionAsync(Guid id);
    Task<PromotionDto> UpdatePromotionAsync(Guid id, CreatePromotionDto dto);
    Task<PromotionDto> ReviewPromotionAsync(ReviewPromotionDto dto);
    Task<PromotionApplicationResultDto> ApplyVoucherAsync(string voucherCode, Guid orderId);
}