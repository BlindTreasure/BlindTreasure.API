using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces.Commons;

public interface IDataAnalyzerService
{
    Task<List<UserDto>> GetUsersForAiAnalysisAsync();
    Task<List<Product>> GetProductsAiAnalysisAsync();
}