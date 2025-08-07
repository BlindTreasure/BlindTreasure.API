using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.DTOs.UnboxLogDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IUnboxingService
{
    Task<UnboxResultDto> UnboxAsync(Guid customerBlindBoxId);
    Task<List<ProbabilityConfig>> GetApprovedProbabilitiesAsync(Guid blindBoxId);
    Task<Pagination<UnboxLogDto>> GetLogsAsync(PaginationParameter param, Guid? userId, Guid? productId);
}