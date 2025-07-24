using BlindTreasure.Domain.DTOs.TradeHistoryDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface ITradingService
{
    Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId);
    Task<TradeRequestDto> CreateTradeRequestAsync(CreateTradeRequestDto request);
    Task<TradeRequestDto> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted);
    Task<Pagination<TradeHistoryDto>> GetAllTradeHistoriesAsync(TradeHistoryQueryParameter param);
    Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId);
    Task ReleaseHeldItemsAsync();
}