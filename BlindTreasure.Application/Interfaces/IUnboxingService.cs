﻿using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.DTOs.UnboxLogDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces;

public interface IUnboxingService
{
    Task<UnboxResultDto> UnboxAsync(Guid customerBlindBoxId);
    Task<List<ProbabilityConfig>> GetApprovedProbabilitiesAsync(Guid blindBoxId);
    Task<List<UnboxLogDto>> GetLogsAsync(Guid? userId, Guid? productId);
}