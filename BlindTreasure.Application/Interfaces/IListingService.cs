using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IListingService
{
    Task<ListingDetailDto> CreateListingAsync(CreateListingRequestDto dto);
    Task<ListingDetailDto> GetByIdAsync(Guid listingId);
    Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync();
    Task<Pagination<ListingDetailDto>> GetAllListingsAsync(ListingQueryParameter param);
    Task ReportListingAsync(Guid listingId, string reason);
    Task<ListingDetailDto> CloseListingAsync(Guid listingId);
}