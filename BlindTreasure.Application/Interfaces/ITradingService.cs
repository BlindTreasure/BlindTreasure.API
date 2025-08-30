using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.TradeHistoryDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface ITradingService
{
    Task<Pagination<TradeRequestDto>> GetAllTradeRequests(PaginationParameter param,
        bool onlyActive = false);
    Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId);
    Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, CreateTradeRequestDto request);
    Task<TradeRequestDto> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted);

    Task<Pagination<TradeHistoryDto>> GetTradeHistoriesAsync(TradeHistoryQueryParameter param,
        bool onlyMine = false);

    Task<TradeRequestDto> ForceTimeoutTradeRequestAsync(Guid tradeRequestId);
    Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId);
    Task ReleaseHeldItemsAsync();
    Task<TradeRequestDto> GetTradeRequestByIdAsync(Guid tradeRequestId);
}