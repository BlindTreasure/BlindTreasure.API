using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

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


    public async Task<Pagination<ListingDetailDto>> GetAllListingsAsync(ListingQueryParameter param)
    {
        _logger.Info(
            $"[GetAllListingsAsync] Page: {param.PageIndex}, Size: {param.PageSize}, Status: {param.Status}, IsFree: {param.IsFree}");

        var query = _unitOfWork.Listings.GetQueryable()
            .Include(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(l => l.InventoryItem.User)
            .Where(l => !l.IsDeleted)
            .AsNoTracking();

        if (param.Status.HasValue)
            query = query.Where(l => l.Status == param.Status.Value);

        if (param.IsFree.HasValue)
            query = query.Where(l => l.IsFree == param.IsFree.Value);

        var count = await query.CountAsync();

        var listings = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var listingDtos = listings
            .Select(l =>
            {
                var dto = _mapper.Map<Listing, ListingDetailDto>(l);
                dto.ProductName = l.InventoryItem?.Product?.Name ?? "Unknown";
                dto.ProductImage = l.InventoryItem?.Product?.ImageUrls?.FirstOrDefault() ?? "";
                return dto;
            })
            .ToList();

        return new Pagination<ListingDetailDto>(listingDtos, count, param.PageIndex, param.PageSize);
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

        return items.Select(item =>
        {
            var dto = _mapper.Map<InventoryItem, InventoryItemDto>(item);
            return dto;
        }).ToList();
    }

    public async Task<ListingDto> CreateListingAsync(CreateListingRequestDto dto)
    {
        var userId = _claimsService.CurrentUserId;

        // Kiểm tra tính toàn vẹn của item trước khi đăng tin
        await EnsureItemCanBeListedAsync(dto.InventoryId, userId);

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
            Description = dto.Description, // Lưu mô tả vào Listing
            ListedAt = DateTime.UtcNow,
            Status = ListingStatus.Active,
            TradeStatus = TradeStatus.Pending
        };

        await _unitOfWork.Listings.AddAsync(listing);
        await _unitOfWork.SaveChangesAsync();

        var listingDto = _mapper.Map<Listing, ListingDto>(listing);
        listingDto.ProductName = inventory.Product?.Name ?? "Unknown";
        listingDto.ProductImage = inventory.Product?.ImageUrls?.FirstOrDefault() ?? "";
        listingDto.Description = listing.Description; // Đảm bảo trả về mô tả

        return listingDto;
    }

    public async Task<bool> CloseListingAsync(Guid listingId)
    {
        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId);
        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        var userId = _claimsService.CurrentUserId;
        if (listing.InventoryItem.UserId != userId)
            throw ErrorHelper.Forbidden("Bạn không có quyền đóng listing này.");

        listing.Status = ListingStatus.Sold;
        await _unitOfWork.Listings.Update(listing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Tạo báo cáo listing
    /// </summary>
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


    private async Task EnsureItemCanBeListedAsync(Guid inventoryId, Guid userId)
    {
        // Kiểm tra xem item có tồn tại không
        var inventoryItem = await _unitOfWork.InventoryItems.FirstOrDefaultAsync(
            x => x.Id == inventoryId &&
                 x.UserId == userId &&
                 !x.IsDeleted &&
                 x.Status == InventoryItemStatus.Available,
            i => i.Listings
        );

        if (inventoryItem == null)
            throw ErrorHelper.NotFound("Không tìm thấy vật phẩm hợp lệ để tạo listing.");

        // Kiểm tra xem item có listing đang hoạt động không
        if (inventoryItem.Listings?.Any(l => l.Status == ListingStatus.Active) == true)
            throw ErrorHelper.Conflict("Vật phẩm này đã có một listing đang hoạt động.");

        // Kiểm tra xem item có bị khóa trong giao dịch nào không
        var ongoingTradeRequest = await _unitOfWork.TradeRequests.FirstOrDefaultAsync(t =>
            t.OfferedInventoryId == inventoryId &&
            t.Status == TradeRequestStatus.Pending
        );

        if (ongoingTradeRequest != null)
            throw ErrorHelper.Conflict("Vật phẩm này đang có giao dịch chờ xử lý.");
    }
}