using BlindTreasure.Domain.DTOs.PromotionDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IPromotionService
{
    Task<PromotionDto> CreatePromotionAsync(CreatePromotionDto dto);
    
}