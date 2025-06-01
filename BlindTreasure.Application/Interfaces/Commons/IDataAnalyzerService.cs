using BlindTreasure.Domain.DTOs.UserDTOs;

namespace BlindTreasure.Application.Interfaces.Commons;

public interface IDataAnalyzerService
{
    Task<List<UserDto>> GetUsersForAiAnalysisAsync();
}