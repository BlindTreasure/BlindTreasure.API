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
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ListingService(IClaimsService claimsService, ILoggerService logger,
        IMapperService mapper, IUnitOfWork unitOfWork)
    {
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Tạo listing mới, hỗ trợ cho free hoặc trade item.
    /// </summary>
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
            IsFree = dto.IsFree,
            DesiredItemId = dto.IsFree ? null : dto.DesiredItemId,
            DesiredItemName = dto.IsFree ? null : dto.DesiredItemName,
            ListedAt = DateTime.UtcNow,
            Status = ListingStatus.Active,
            TradeStatus = TradeStatus.Pending
        };

        await _unitOfWork.Listings.AddAsync(listing);
        await _unitOfWork.SaveChangesAsync();

        var listingDto = _mapper.Map<Listing, ListingDto>(listing);
        listingDto.ProductName = inventory.Product?.Name ?? "Unknown";
        listingDto.ProductImage = inventory.Product?.ImageUrls?.FirstOrDefault() ?? "";

        return listingDto;
    }

    public async Task ReportListingAsync(Guid listingId, string reason)
    {
        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId);
        if (listing == null)
            throw ErrorHelper.NotFound("Không tìm thấy listing để báo cáo.");

        var report = new ListingReport
        {
            ListingId = listingId,
            UserId = _claimsService.CurrentUserId,
            Reason = reason,
            ReportedAt = DateTime.UtcNow
        };

        await _unitOfWork.ListingReports.AddAsync(report);
        await _unitOfWork.SaveChangesAsync();
    }


    /// <summary>
    /// Lấy danh sách item có thể tạo listing.
    /// </summary>
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


    /// <summary>
    /// Hết hạn listing sau 30 ngày.
    /// </summary>
    public async Task<int> ExpireOldListingsAsync()
    {
        var now = DateTime.UtcNow;
        var expiredThreshold = now.AddDays(-30);

        var expiredListings = await _unitOfWork.Listings.GetAllAsync(l => l.Status == ListingStatus.Active &&
                                                                          l.ListedAt < expiredThreshold &&
                                                                          !l.IsDeleted);

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
}