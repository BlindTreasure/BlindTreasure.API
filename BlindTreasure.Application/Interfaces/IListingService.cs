using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IListingService
{
    Task<ListingDto> CreateListingAsync(CreateListingRequestDto dto);
    Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync();
    Task ReportListingAsync(Guid listingId, string reason);
    Task<int> ExpireOldListingsAsync();
}