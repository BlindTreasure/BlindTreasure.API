using BlindTreasure.Domain.DTOs.TradeRequestDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface ITradingService
{
    Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId);
    Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, Guid? offeredInventoryId);
    Task<bool> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted);
    Task<bool> ExpireDealAsync(Guid tradeRequestId);
    Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId);
    // Task<bool> ConfirmDealAsync(Guid tradeRequestId);
}