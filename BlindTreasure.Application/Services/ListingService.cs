using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ListingService : IListingService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ListingService(IClaimsService claimsService, ILoggerService logger,
        IMapperService mapper, IUnitOfWork unitOfWork, ICacheService cacheService)
    {
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<ListingDetailDto> GetListingByIdAsync(Guid id)
    {
        var cacheKey = CacheKeys.GetListingDetail(id);

        // Kiểm tra cache trước
        var cachedListing = await _cacheService.GetAsync<ListingDetailDto>(cacheKey);
        if (cachedListing != null)
        {
            _logger.Info($"[GetByIdAsync] Cache hit cho bài đăng: {id}");
            return cachedListing;
        }

        _logger.Info($"[GetByIdAsync] Cache miss cho bài đăng: {id}, truy vấn database");

        var listing = await _unitOfWork.Listings
            .GetQueryable()
            .Include(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(l => l.InventoryItem.User)
            .Where(l => !l.IsDeleted && l.Id == id)
            .FirstOrDefaultAsync();

        if (listing == null)
            throw ErrorHelper.NotFound("Rất tiếc, bài đăng bạn đang tìm kiếm không tồn tại hoặc đã bị xóa.");

        var result = MapListingToDto(listing);

        // Lưu vào cache với thời gian khác nhau dựa trên status
        var cacheDuration = listing.Status == ListingStatus.Active
            ? CacheDurations.ActiveListing
            : CacheDurations.InactiveListing;

        await _cacheService.SetAsync(cacheKey, result, cacheDuration);
        _logger.Info($"[GetByIdAsync] Đã cache bài đăng {id} với duration: {cacheDuration}");

        return result;
    }

    public async Task<Pagination<ListingDetailDto>> GetAllListingsAsync(ListingQueryParameter param)
    {
        var query = _unitOfWork.Listings.GetQueryable()
            .Include(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(l => l.InventoryItem.User)
            .Where(l => !l.IsDeleted)
            .AsNoTracking();

        // Áp dụng filters
        query = ApplyFilters(query, param);

        var count = await query.CountAsync();

        var listings = await query
            .OrderByDescending(l => l.ListedAt)
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var listingDtos = listings.Select(MapListingToDto).ToList();

        return new Pagination<ListingDetailDto>(listingDtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync()
    {
        var userId = _claimsService.CurrentUserId;
        var cacheKey = CacheKeys.GetUserAvailableItems(userId);

        var cachedItems = await _cacheService.GetAsync<List<InventoryItemDto>>(cacheKey);
        if (cachedItems != null)
            return cachedItems;

        var items = await GetUserListableItemsAsync(userId);

        var result = items.Select(item =>
        {
            var dto = _mapper.Map<InventoryItem, InventoryItemDto>(item);
            dto.ProductName = item.Product?.Name;
            dto.Image = item.Product?.ImageUrls?.FirstOrDefault() ?? "";
            dto.IsOnHold = item.HoldUntil.HasValue && item.HoldUntil > DateTime.UtcNow;
            dto.HasActiveListing = item.Listings?.Any(l => l.Status == ListingStatus.Active) ?? false;
            dto.Status = item.Status;
            return dto;
        }).ToList();

        await _cacheService.SetAsync(cacheKey, result, CacheDurations.UserItems);
        return result;
    }

    public async Task<ListingDetailDto> CreateListingAsync(CreateListingRequestDto dto)
    {
        var userId = _claimsService.CurrentUserId;

        // Kiểm tra điều kiện tạo listing
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
            throw ErrorHelper.NotFound(
                $"Rất tiếc, vật phẩm tồn kho với ID {dto.InventoryId} không tồn tại, đã bị xóa, hoặc bạn không phải là chủ sở hữu.");

        if (inventory.Listings?.Any(l => l.Status == ListingStatus.Active) == true)
            throw ErrorHelper.Conflict(
                $"Vật phẩm này (ID: {dto.InventoryId}) hiện đang có một bài đăng đang hoạt động. Mỗi vật phẩm chỉ được phép có một bài đăng hoạt động tại một thời điểm.");

        var listing = new Listing
        {
            InventoryId = inventory.Id,
            IsFree = dto.IsFree,
            Description = dto.Description,
            ListedAt = DateTime.UtcNow,
            Status = ListingStatus.Active,
            TradeStatus = TradeStatus.Pending
        };

        await _unitOfWork.Listings.AddAsync(listing);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache của user items
        await InvalidateUserItemsCache(userId);

        _logger.Info($"[CreateListing] Đã tạo bài đăng {listing.Id} và xóa cache");

        // Load đầy đủ thông tin bài đăng vừa tạo
        return await GetListingByIdAsync(listing.Id);
    }

    public async Task<ListingDetailDto> CloseListingAsync(Guid listingId)
    {
        var listing = await _unitOfWork.Listings
            .GetQueryable()
            .Include(l => l.InventoryItem)
            .FirstOrDefaultAsync(l => l.Id == listingId && !l.IsDeleted);

        if (listing == null)
            throw ErrorHelper.NotFound("Rất tiếc, bài đăng này không tồn tại hoặc đã bị gỡ bỏ.");

        var userId = _claimsService.CurrentUserId;
        if (listing.InventoryItem.UserId != userId)
            throw ErrorHelper.Forbidden(
                "Bạn không có quyền đóng bài đăng này vì bạn không phải là chủ sở hữu vật phẩm.");

        listing.Status = ListingStatus.Sold;
        await _unitOfWork.Listings.Update(listing);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache
        await InvalidateListingCache(listingId);
        await InvalidateUserItemsCache(userId);

        _logger.Info($"[CloseListing] Đã đóng bài đăng {listingId} và xóa cache");

        return await GetListingByIdAsync(listingId);
    }

    public async Task ReportListingAsync(Guid listingId, string reason)
    {
        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId);
        if (listing == null)
            throw ErrorHelper.NotFound("Rất tiếc, không tìm thấy bài đăng để báo cáo.");

        var report = new ListingReport
        {
            ListingId = listingId,
            UserId = _claimsService.CurrentUserId,
            Reason = reason,
            ReportedAt = DateTime.UtcNow
        };

        await _unitOfWork.ListingReports.AddAsync(report);
        await _unitOfWork.SaveChangesAsync();

        _logger.Info($"[ReportListing] User {_claimsService.CurrentUserId} đã báo cáo bài đăng {listingId}");
    }

    #region Cache Management

    private static class CacheKeys
    {
        private const string PREFIX = "listing:";

        public static string GetListingDetail(Guid listingId)
        {
            return $"{PREFIX}detail:{listingId}";
        }

        public static string GetUserAvailableItems(Guid userId)
        {
            return $"{PREFIX}user-items:{userId}";
        }
    }

    private static class CacheDurations
    {
        public static readonly TimeSpan ActiveListing = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan InactiveListing = TimeSpan.FromHours(1);
        public static readonly TimeSpan UserItems = TimeSpan.FromMinutes(10);
    }

    private async Task InvalidateListingCache(Guid listingId)
    {
        var cacheKey = CacheKeys.GetListingDetail(listingId);
        await _cacheService.RemoveAsync(cacheKey);
        _logger.Info($"[Cache] Đã xóa cache bài đăng: {listingId}");
    }

    private async Task InvalidateUserItemsCache(Guid userId)
    {
        var cacheKey = CacheKeys.GetUserAvailableItems(userId);
        await _cacheService.RemoveAsync(cacheKey);
        _logger.Info($"[Cache] Đã xóa cache items của user: {userId}");
    }

    #endregion

    #region Private Methods

    private IQueryable<Listing> ApplyFilters(IQueryable<Listing> query, ListingQueryParameter param)
    {
        // Filter theo status
        if (param.Status.HasValue)
            query = query.Where(l => l.Status == param.Status.Value);

        // Filter theo IsFree
        if (param.IsFree.HasValue)
            query = query.Where(l => l.IsFree == param.IsFree.Value);

        // Filter theo owner
        if (param.IsOwnerListings.HasValue)
        {
            var currentUserId = _claimsService.CurrentUserId;
            if (param.IsOwnerListings.Value)
                // Lấy listings của user hiện tại
                query = query.Where(l => l.InventoryItem.UserId == currentUserId);
            else
                // Loại trừ listings của user hiện tại
                query = query.Where(l => l.InventoryItem.UserId != currentUserId);
        }

        // Filter theo userId cụ thể
        if (param.UserId.HasValue)
            query = query.Where(l => l.InventoryItem.UserId == param.UserId.Value);

        // Search theo tên sản phẩm
        if (!string.IsNullOrWhiteSpace(param.SearchByName))
        {
            var searchTerm = param.SearchByName.Trim().ToLower();
            query = query.Where(l => l.InventoryItem.Product.Name.ToLower().Contains(searchTerm));
        }

        // Filter theo CategoryId
        if (param.CategoryId.HasValue)
            query = query.Where(l => l.InventoryItem.Product.CategoryId == param.CategoryId.Value);

        return query;
    }

    private ListingDetailDto MapListingToDto(Listing listing)
    {
        var dto = _mapper.Map<Listing, ListingDetailDto>(listing);

        dto.InventoryId = listing.InventoryId;
        dto.ProductName = listing.InventoryItem?.Product?.Name ?? "Unknown";
        dto.ProductImage = listing.InventoryItem?.Product?.ImageUrls?.FirstOrDefault() ?? "";
        dto.Description = listing.Description;
        dto.AvatarUrl = listing.InventoryItem?.User?.AvatarUrl;

        if (listing.InventoryItem?.User != null)
        {
            dto.OwnerName = listing.InventoryItem.User.FullName ?? listing.InventoryItem.User.Email;
            dto.OwnerId = listing.InventoryItem.User.Id;
        }

        return dto;
    }

    private async Task EnsureItemCanBeListedAsync(Guid inventoryId, Guid userId)
    {
        var items = await GetUserListableItemsAsync(userId, inventoryId);
        if (!items.Any()) throw ErrorHelper.Conflict("Không tìm thấy vật phẩm hợp lệ để tạo bài đăng.");
    }

    /// <summary>
    /// Lấy các item khả dụng để tạo listing hoặc kiểm tra 1 item cụ thể.
    /// </summary>
    private async Task<List<InventoryItem>> GetUserListableItemsAsync(Guid userId, Guid? specificInventoryId = null)
    {
        var query = _unitOfWork.InventoryItems.GetQueryable()
            .Include(i => i.Product)
            .Include(i => i.Listings)
            .Include(i => i.LastTradeHistory)
            .Include(i => i.SourceCustomerBlindBox)
            .Where(x => x.UserId == userId
                        && !x.IsDeleted
                        && x.Status == InventoryItemStatus.Available
                        && x.SourceCustomerBlindBoxId != null); // chỉ từ BlindBox

        if (specificInventoryId.HasValue)
            query = query.Where(x => x.Id == specificInventoryId.Value);

        var items = await query.ToListAsync();

        // Filter thêm: loại bỏ item có listing active hoặc đang trong trade pending
        var result = new List<InventoryItem>();
        foreach (var item in items)
        {
            if (item.Listings?.Any(l => l.Status == ListingStatus.Active) == true)
                continue;

            var hasPendingTrade = await _unitOfWork.TradeRequests
                .GetQueryable()
                .Include(t => t.OfferedItems)
                .Where(t => t.Status == TradeRequestStatus.PENDING)
                .AnyAsync(t => t.OfferedItems.Any(oi => oi.InventoryItemId == item.Id));

            if (hasPendingTrade)
                continue;

            result.Add(item);
        }

        return result;
    }

    #endregion
}