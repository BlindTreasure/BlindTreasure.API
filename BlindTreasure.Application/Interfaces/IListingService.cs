using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IListingService
{
    Task<ListingDto> CreateListingAsync(CreateListingRequestDto dto);
    Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync();
    Task<Pagination<ListingDetailDto>> GetAllListingsAsync(ListingQueryParameter param);
    Task ReportListingAsync(Guid listingId, string reason);

    Task<bool> CloseListingAsync(Guid listingId);
}