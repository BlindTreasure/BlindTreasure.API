using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class ListingService : IListingService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ListingService(ICacheService cacheService, IClaimsService claimsService, ILoggerService logger,
        IMapperService mapper, IUnitOfWork unitOfWork)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<ListingDto> CreateListingAsync(CreateListingRequestDto dto)
    {
        var userId = _claimsService.CurrentUserId;

        var inventory = await _unitOfWork.InventoryItems.FirstOrDefaultAsync(
            x => x.Id == dto.InventoryId &&
                 x.UserId == userId &&
                 !x.IsDeleted &&
                 x.IsFromBlindBox &&
                 x.Status == InventoryItemStatus.Available,
            i => i.Product,
            i => i.Listings
        );

        if (inventory == null)
            throw ErrorHelper.NotFound("Không tìm thấy vật phẩm hợp lệ để tạo listing.");

        if (inventory.Listings?.Any(l => l.Status == ListingStatus.Active) == true)
            throw ErrorHelper.Conflict("Vật phẩm này đã có một listing đang hoạt động.");

        var listing = new Listing
        {
            InventoryId = inventory.Id,
            Price = dto.Price,
            ListedAt = DateTime.UtcNow,
            Status = ListingStatus.Active
        };

        await _unitOfWork.Listings.AddAsync(listing);
        await _unitOfWork.SaveChangesAsync();

        await TrackPriceChangeAsync(inventory.ProductId, dto.Price);

        var listingDto = _mapper.Map<Listing, ListingDto>(listing);
        listingDto.ProductName = inventory.Product?.Name ?? "Unknown";
        listingDto.ProductImage = inventory.Product?.ImageUrls?.FirstOrDefault() ?? "";

        return listingDto;
    }

    public async Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync()
    {
        var userId = _claimsService.CurrentUserId;

        var items = await _unitOfWork.InventoryItems.GetAllAsync(
            x => x.UserId == userId &&
                 x.IsFromBlindBox &&
                 !x.IsDeleted &&
                 x.Status == InventoryItemStatus.Available &&
                 (!x.Listings.Any() || x.Listings.All(l => l.Status != ListingStatus.Active)),
            i => i.Product,
            i => i.Listings
        );

        return items.Select(item => _mapper.Map<InventoryItem, InventoryItemDto>(item)).ToList();
    }

    public async Task<decimal> GetSuggestedPriceAsync(Guid productId)
    {
        var listings = await _unitOfWork.Listings.GetAllAsync(
            l => !l.IsDeleted &&
                 l.Status == ListingStatus.Active &&
                 l.InventoryItem.ProductId == productId,
            l => l.InventoryItem
        );

        if (!listings.Any())
            throw ErrorHelper.NotFound("Chưa có dữ liệu thị trường để gợi ý giá.");

        return Math.Round(listings.Average(x => x.Price), 2);
    }

    public async Task<List<PricePointDto>> GetPriceHistoryAsync(Guid productId)
    {
        var key = $"price-history:{productId}";
        var history = await _cacheService.GetAsync<List<PricePointDto>>(key);

        return history?.OrderBy(h => h.Timestamp).ToList() ?? new List<PricePointDto>();
    }

    #region private methods

    private async Task TrackPriceChangeAsync(Guid productId, decimal price)
    {
        var key = $"price-history:{productId}";
        var now = DateTime.UtcNow;

        var history = await _cacheService.GetAsync<List<PricePointDto>>(key) ?? new List<PricePointDto>();

        history.Add(new PricePointDto
        {
            Timestamp = now,
            Price = price
        });

        // Giữ tối đa 100 bản ghi gần nhất (bảo vệ Redis và tốc độ truy vấn)
        history = history
            .OrderByDescending(h => h.Timestamp)
            .Take(100)
            .OrderBy(h => h.Timestamp)
            .ToList();

        await _cacheService.SetAsync(key, history, TimeSpan.FromDays(7));
    }

    public async Task<int> ExpireOldListingsAsync()
    {
        var now = DateTime.UtcNow;
        var expiredThreshold = now.AddDays(-30);

        // Truy vấn các listing Active quá 30 ngày
        var expiredListings = await _unitOfWork.Listings.GetAllAsync(l => l.Status == ListingStatus.Active &&
                                                                          l.ListedAt < expiredThreshold &&
                                                                          !l.IsDeleted
        );

        if (!expiredListings.Any())
            return 0;

        foreach (var listing in expiredListings)
        {
            listing.Status = ListingStatus.Expired;
            listing.UpdatedAt = now;
            listing.UpdatedBy = _claimsService.CurrentUserId;
        }

        await _unitOfWork.Listings.UpdateRange(expiredListings);
        await _unitOfWork.SaveChangesAsync();

        return expiredListings.Count;
    }

    #endregion
}