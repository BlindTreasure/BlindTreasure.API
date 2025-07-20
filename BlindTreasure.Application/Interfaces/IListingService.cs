using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IListingService
{
    Task<ListingDto> CreateListingAsync(CreateListingRequestDto dto);
    Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync();
    Task ReportListingAsync(Guid listingId, string reason);
    Task<int> ExpireOldListingsAsync();
    Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, Guid offeredInventoryId);
    Task<bool> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted);
    Task<bool> CloseListingAsync(Guid listingId);
    Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId);
}