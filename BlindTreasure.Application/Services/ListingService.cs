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
        var cacheKey = CacheKeyManager.GetListingDetailKey(id);

        // Kiểm tra cache trước
        var cachedListing = await _cacheService.GetAsync<ListingDetailDto>(cacheKey);
        if (cachedListing != null)
        {
            _logger.Info($"[GetByIdAsync] Đã lấy listing từ cache: {id}");
            return cachedListing;
        }

        _logger.Info($"[GetByIdAsync] Cache miss cho listing: {id}, đang truy vấn database");

        var listing = await _unitOfWork.Listings
            .GetQueryable()
            .Include(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(l => l.InventoryItem.User)
            .Where(l => !l.IsDeleted && l.Id == id)
            .FirstOrDefaultAsync();

        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        var result = MapListingToDto(listing);

        // Lưu vào cache
        await _cacheService.SetAsync(cacheKey, result, CacheKeyManager.LISTING_CACHE_DURATION);
        _logger.Info($"[GetByIdAsync] Đã lưu listing {id} vào cache");

        return result;
    }

    public async Task<Pagination<ListingDetailDto>> GetAllListingsAsync(ListingQueryParameter param)
    {
        var currentUserId = _claimsService.CurrentUserId;

        _logger.Info(
            $"[GetAllListingsAsync] Page: {param.PageIndex}, Size: {param.PageSize}, Status: {param.Status}, IsFree: {param.IsFree}, IsOwnerListings: {param.IsOwnerListings}, UserId: {param.UserId}, CurrentUserId: {currentUserId}");

        var query = _unitOfWork.Listings.GetQueryable()
            .Include(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(l => l.InventoryItem.User)
            .Where(l => !l.IsDeleted)
            .AsNoTracking();

        // Mặc định loại trừ listings của current user, trừ khi param.IsOwnerListings = true
        if (!(param.IsOwnerListings.HasValue && param.IsOwnerListings.Value))
        {
            query = query.Where(l => l.InventoryItem.UserId != currentUserId);
            _logger.Info($"[GetAllListingsAsync] Đã loại trừ listings của current user: {currentUserId}");
        }

        // Áp dụng các filter
        query = ApplyFilters(query, param);

        var count = await query.CountAsync();

        var listings = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var listingDtos = listings.Select(MapListingToDto).ToList();

        _logger.Info($"[GetAllListingsAsync] Đã tìm thấy {count} listings phù hợp với filter");

        return new Pagination<ListingDetailDto>(listingDtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync()
    {
        var userId = _claimsService.CurrentUserId;
        var cacheKey = CacheKeyManager.GetAvailableItemsKey(userId);

        // Check cache first
        var cachedItems = await _cacheService.GetAsync<List<InventoryItemDto>>(cacheKey);
        if (cachedItems != null)
        {
            _logger.Info($"[GetAvailableItemsForListingAsync] Retrieved items from cache for user: {userId}");
            return cachedItems;
        }

        _logger.Info($"[GetAvailableItemsForListingAsync] Cache miss for user: {userId}, querying database");

        // Get ALL inventory items for the user (no status filtering)
        var items = await _unitOfWork.InventoryItems.GetAllAsync(
            x => x.UserId == userId && !x.IsDeleted,
            i => i.Product,
            i => i.Listings,
            i => i.LastTradeHistory
        );

        var result = items.Select(item =>
        {
            var dto = _mapper.Map<InventoryItem, InventoryItemDto>(item);
            dto.Id = item.Id;

            // Product info
            if (item.Product != null)
            {
                dto.ProductName = item.Product.Name;
                dto.Image = item.Product.ImageUrls?.FirstOrDefault() ?? "";
            }

            // Trade hold status (3 days after trade)
            dto.IsOnHold = item.HoldUntil.HasValue && item.HoldUntil.Value > DateTime.UtcNow;

            // Active listing check
            dto.HasActiveListing = item.Listings?.Any(l => l.Status == ListingStatus.Active) ?? false;

            // Additional status flag for frontend
            dto.Status = item.Status;

            return dto;
        }).ToList();

        // Cache the results
        await _cacheService.SetAsync(cacheKey, result, CacheKeyManager.AVAILABLE_ITEMS_CACHE_DURATION);
        _logger.Info($"[GetAvailableItemsForListingAsync] Cached items for user: {userId}");

        return result;
    }

    public async Task<ListingDetailDto> CreateListingAsync(CreateListingRequestDto dto)
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
            Description = dto.Description,
            ListedAt = DateTime.UtcNow,
            Status = ListingStatus.Active,
            TradeStatus = TradeStatus.Pending
        };

        await _unitOfWork.Listings.AddAsync(listing);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache liên quan
        await InvalidateListingRelatedCacheAsync(userId);
        _logger.Info("[CreateListingAsync] Đã xóa cache liên quan sau khi tạo listing mới");

        // Sử dụng GetByIdAsync để đảm bảo dữ liệu đồng nhất
        var createdListing = await GetListingByIdAsync(listing.Id);

        return createdListing;
    }

    public async Task<ListingDetailDto> CloseListingAsync(Guid listingId)
    {
        var listing = await _unitOfWork.Listings
            .GetQueryable()
            .Include(l => l.InventoryItem)
            .FirstOrDefaultAsync(l => l.Id == listingId && !l.IsDeleted);

        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        var userId = _claimsService.CurrentUserId;
        if (listing.InventoryItem.UserId != userId)
            throw ErrorHelper.Forbidden("Bạn không có quyền đóng listing này.");

        listing.Status = ListingStatus.Sold;
        await _unitOfWork.Listings.Update(listing);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache liên quan đến listing này
        await InvalidateListingCacheAsync(listingId, userId);
        _logger.Info($"[CloseListingAsync] Đã xóa cache cho listing {listingId} sau khi đóng");

        return await GetListingByIdAsync(listingId);
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

    #region Caching

    private class CacheKeyManager
    {
        // Cache keys
        public const string ALL_LISTINGS_CACHE_KEY = "listings:all:{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}";
        public const string LISTING_DETAIL_CACHE_KEY = "listing:detail:{0}";
        public const string AVAILABLE_ITEMS_CACHE_KEY = "listings:available-items:{0}";

        // Cache durations
        public static readonly TimeSpan LISTING_CACHE_DURATION = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan AVAILABLE_ITEMS_CACHE_DURATION = TimeSpan.FromMinutes(10);

        // Generate specific cache keys
        public static string GetListingDetailKey(Guid listingId)
        {
            return string.Format(LISTING_DETAIL_CACHE_KEY, listingId);
        }

        public static string GetAvailableItemsKey(Guid userId)
        {
            return string.Format(AVAILABLE_ITEMS_CACHE_KEY, userId);
        }

        public static string GetAllListingsKey(ListingQueryParameter param)
        {
            return string.Format(ALL_LISTINGS_CACHE_KEY,
                param.PageIndex,
                param.PageSize,
                param.Status?.ToString() ?? "null",
                param.IsFree?.ToString() ?? "null",
                param.IsOwnerListings?.ToString() ?? "null",
                param.UserId?.ToString() ?? "null",
                param.SearchByName ?? "null",
                param.CategoryId?.ToString() ?? "null");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Xóa cache cho một listing cụ thể và danh sách listing của người dùng
    /// </summary>
    private async Task InvalidateListingCacheAsync(Guid listingId, Guid userId)
    {
        // Xóa cache chi tiết của listing
        var detailCacheKey = CacheKeyManager.GetListingDetailKey(listingId);
        await _cacheService.RemoveAsync(detailCacheKey);
        _logger.Info($"[InvalidateListingCacheAsync] Đã xóa cache chi tiết cho listing {listingId}");

        // Xóa cache vật phẩm có thể listing của user
        await InvalidateListingRelatedCacheAsync(userId);
    }

    /// <summary>
    ///     Xóa tất cả cache liên quan đến listings và vật phẩm có thể listing
    /// </summary>
    private async Task InvalidateListingRelatedCacheAsync(Guid userId)
    {
        // Xóa cache danh sách listings
        await _cacheService.RemoveByPatternAsync("listings:all:*");
        _logger.Info("[InvalidateListingRelatedCacheAsync] Đã xóa cache danh sách listings");

        // Xóa cache vật phẩm có thể listing của user
        var availableItemsCacheKey = CacheKeyManager.GetAvailableItemsKey(userId);
        await _cacheService.RemoveAsync(availableItemsCacheKey);
        _logger.Info($"[InvalidateListingRelatedCacheAsync] Đã xóa cache vật phẩm có thể listing cho user {userId}");
    }

    private IQueryable<Listing> ApplyFilters(IQueryable<Listing> query, ListingQueryParameter param)
    {
        // Filter theo status
        if (param.Status.HasValue)
            query = query.Where(l => l.Status == param.Status.Value);

        // Filter theo IsFree
        if (param.IsFree.HasValue)
            query = query.Where(l => l.IsFree == param.IsFree.Value);

        // Filter theo owner (listings của chính user hiện tại)
        if (param.IsOwnerListings.HasValue && param.IsOwnerListings.Value)
        {
            var currentUserId = _claimsService.CurrentUserId;
            query = query.Where(l => l.InventoryItem.UserId == currentUserId);

            _logger.Info($"[ApplyFilters] Filtering by owner: {currentUserId}");
        }

        // Filter theo userId cụ thể
        if (param.UserId.HasValue)
        {
            query = query.Where(l => l.InventoryItem.UserId == param.UserId.Value);

            _logger.Info($"[ApplyFilters] Filtering by userId: {param.UserId.Value}");
        }

        // Filter theo tên sản phẩm (search)
        if (!string.IsNullOrWhiteSpace(param.SearchByName))
        {
            var searchTerm = param.SearchByName.Trim().ToLower();
            query = query.Where(l => l.InventoryItem.Product.Name.ToLower().Contains(searchTerm));

            _logger.Info($"[ApplyFilters] Filtering by product name containing: {param.SearchByName}");
        }

        // Filter theo CategoryId
        if (param.CategoryId.HasValue)
        {
            query = query.Where(l => l.InventoryItem.Product.CategoryId == param.CategoryId.Value);

            _logger.Info($"[ApplyFilters] Filtering by categoryId: {param.CategoryId.Value}");
        }

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
        // Thêm các thông tin khác nếu cần
        if (listing.InventoryItem?.User != null)
            dto.OwnerName = listing.InventoryItem.User.FullName ?? listing.InventoryItem.User.FullName;

        return dto;
    }

    private async Task EnsureItemCanBeListedAsync(Guid inventoryId, Guid userId)
    {
        _logger.Info($"[EnsureItemCanBeListedAsync] Bắt đầu kiểm tra vật phẩm {inventoryId} của người dùng {userId}");

        // Kiểm tra vật phẩm có tồn tại và hợp lệ không
        var inventoryItem = await _unitOfWork.InventoryItems.FirstOrDefaultAsync(
            x => x.Id == inventoryId &&
                 x.UserId == userId &&
                 !x.IsDeleted &&
                 x.Status == InventoryItemStatus.Available,
            i => i.Listings
        );

        if (inventoryItem == null)
        {
            _logger.Warn(
                $"[EnsureItemCanBeListedAsync] Không tìm thấy vật phẩm {inventoryId} hoặc vật phẩm không hợp lệ cho người dùng {userId}");
            throw ErrorHelper.NotFound("Không tìm thấy vật phẩm hợp lệ để tạo listing.");
        }

        _logger.Info(
            $"[EnsureItemCanBeListedAsync] Đã tìm thấy vật phẩm {inventoryId}: {inventoryItem.Product?.Name ?? "Unknown"} của người dùng {userId}");

        // Kiểm tra vật phẩm đã có listing đang hoạt động chưa
        if (inventoryItem.Listings?.Any(l => l.Status == ListingStatus.Active) == true)
        {
            var activeListing = inventoryItem.Listings.First(l => l.Status == ListingStatus.Active);
            _logger.Warn(
                $"[EnsureItemCanBeListedAsync] Vật phẩm {inventoryId} đã có listing {activeListing.Id} đang hoạt động");
            throw ErrorHelper.Conflict("Vật phẩm này đã có một listing đang hoạt động.");
        }

        _logger.Info($"[EnsureItemCanBeListedAsync] Vật phẩm {inventoryId} không có listing đang hoạt động");

        // Kiểm tra vật phẩm có đang trong giao dịch nào không
        try
        {
            _logger.Info(
                $"[EnsureItemCanBeListedAsync] Kiểm tra vật phẩm {inventoryId} trong các giao dịch đang chờ xử lý");

            var ongoingTradeRequest = await _unitOfWork.TradeRequests
                .GetQueryable()
                .Include(t => t.OfferedItems)
                .Where(t => t.Status == TradeRequestStatus.PENDING)
                .AnyAsync(t => t.OfferedItems.Any(item => item.InventoryItemId == inventoryId));

            if (ongoingTradeRequest)
            {
                _logger.Warn($"[EnsureItemCanBeListedAsync] Vật phẩm {inventoryId} đang có giao dịch chờ xử lý");
                throw ErrorHelper.Conflict("Vật phẩm này đang có giao dịch chờ xử lý.");
            }

            _logger.Info($"[EnsureItemCanBeListedAsync] Vật phẩm {inventoryId} không có giao dịch đang chờ xử lý");
        }
        catch (Exception ex)
        {
            _logger.Error(
                $"[EnsureItemCanBeListedAsync] Lỗi khi kiểm tra giao dịch cho vật phẩm {inventoryId}: {ex.Message}");
            throw ErrorHelper.Internal("Lỗi khi kiểm tra trạng thái giao dịch của vật phẩm.");
        }

        _logger.Success($"[EnsureItemCanBeListedAsync] Vật phẩm {inventoryId} đủ điều kiện để tạo listing");
    }

    #endregion
}