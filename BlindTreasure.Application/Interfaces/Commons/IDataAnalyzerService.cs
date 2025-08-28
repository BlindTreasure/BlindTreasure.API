using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces.Commons;

public interface IDataAnalyzerService
{
    Task<List<UserDto>> GetUsersForAiAnalysisAsync();
    Task<List<Product>> GetProductsAiAnalysisAsync();
    Task<List<ProductTrendingStatDto>> GetTrendingProductsForAiAsync();
    Task<List<OrderDto>> GetMyOrdersForAiAsync(int limit = 5);
}