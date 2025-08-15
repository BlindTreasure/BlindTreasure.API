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
        var currentUserId = _claimsService.CurrentUserId;

        _logger.Info($"[GetAllListingsAsync] Truy vấn bài đăng với params: {param}");

        var query = _unitOfWork.Listings.GetQueryable()
            .Include(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(l => l.InventoryItem.User)
            .Where(l => !l.IsDeleted)
            .AsNoTracking();

        // Mặc định loại trừ listings của current user
        if (!(param.IsOwnerListings.HasValue && param.IsOwnerListings.Value))
            query = query.Where(l => l.InventoryItem.UserId != currentUserId);

        // Áp dụng filters
        query = ApplyFilters(query, param);

        var count = await query.CountAsync();

        var listings = await query
            .OrderByDescending(l => l.ListedAt)
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var listingDtos = listings.Select(MapListingToDto).ToList();

        _logger.Info($"[GetAllListingsAsync] Tìm thấy {count} bài đăng");

        return new Pagination<ListingDetailDto>(listingDtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<List<InventoryItemDto>> GetAvailableItemsForListingAsync()
    {
        var userId = _claimsService.CurrentUserId;
        var cacheKey = CacheKeys.GetUserAvailableItems(userId);

        // Check cache
        var cachedItems = await _cacheService.GetAsync<List<InventoryItemDto>>(cacheKey);
        if (cachedItems != null)
        {
            _logger.Info($"[GetAvailableItems] Cache hit cho user: {userId}");
            return cachedItems;
        }

        _logger.Info($"[GetAvailableItems] Cache miss cho user: {userId}");

        // Lấy tất cả inventory items của user
        var items = await _unitOfWork.InventoryItems.GetAllAsync(
            x => x.UserId == userId && !x.IsDeleted,
            i => i.Product,
            i => i.Listings,
            i => i.LastTradeHistory
        );

        var result = items.Select(item =>
            {
                var dto = _mapper.Map<InventoryItem, InventoryItemDto>(item);

                // Thông tin sản phẩm
                if (item.Product != null)
                {
                    dto.ProductName = item.Product.Name;
                    dto.Image = item.Product.ImageUrls?.FirstOrDefault() ?? "";
                }

                // Trạng thái hold sau giao dịch (3 ngày)
                dto.IsOnHold = item.HoldUntil.HasValue && item.HoldUntil.Value > DateTime.UtcNow;

                // Kiểm tra có listing active không
                dto.HasActiveListing = item.Listings?.Any(l => l.Status == ListingStatus.Active) ?? false;

                dto.Status = item.Status;

                return dto;
            })
            .OrderBy(item => item.Status != InventoryItemStatus.Available) // Sắp xếp Available lên đầu
            .ThenBy(item => item.Status) // Sắp xếp các trạng thái còn lại theo thứ tự enum
            .ToList();

        // Cache kết quả
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
            throw ErrorHelper.Forbidden("Bạn không có quyền đóng bài đăng này vì bạn không phải là chủ sở hữu vật phẩm.");

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
        private const string PREFIX = "bai_dang:";

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
        if (param.IsOwnerListings.HasValue && param.IsOwnerListings.Value)
        {
            var currentUserId = _claimsService.CurrentUserId;
            query = query.Where(l => l.InventoryItem.UserId == currentUserId);
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
            dto.OwnerName = listing.InventoryItem.User.FullName ?? listing.InventoryItem.User.Email;

        return dto;
    }

    private async Task EnsureItemCanBeListedAsync(Guid inventoryId, Guid userId)
    {
        _logger.Info($"[EnsureItemCanBeListed] Kiểm tra item {inventoryId} của user {userId}");

        // Kiểm tra vật phẩm tồn tại và hợp lệ
        var inventoryItem = await _unitOfWork.InventoryItems.FirstOrDefaultAsync(
            x => x.Id == inventoryId &&
                 x.UserId == userId &&
                 !x.IsDeleted &&
                 x.Status == InventoryItemStatus.Available,
            i => i.Listings
        );

        if (inventoryItem == null)
        {
            _logger.Warn($"[EnsureItemCanBeListed] Không tìm thấy item {inventoryId} hợp lệ");
            throw ErrorHelper.NotFound("Không tìm thấy vật phẩm hợp lệ để tạo bài đăng.");
        }

        // Kiểm tra bài đăng active
        if (inventoryItem.Listings?.Any(l => l.Status == ListingStatus.Active) == true)
        {
            _logger.Warn($"[EnsureItemCanBeListed] Item {inventoryId} đã có bài đăng active");
            throw ErrorHelper.Conflict("Vật phẩm này đã có bài đăng đang hoạt động.");
        }

        // Kiểm tra giao dịch pending
        var hasPendingTrade = await _unitOfWork.TradeRequests
            .GetQueryable()
            .Include(t => t.OfferedItems)
            .Where(t => t.Status == TradeRequestStatus.PENDING)
            .AnyAsync(t => t.OfferedItems.Any(item => item.InventoryItemId == inventoryId));

        if (hasPendingTrade)
        {
            _logger.Warn($"[EnsureItemCanBeListed] Item {inventoryId} đang có giao dịch pending");
            throw ErrorHelper.Conflict("Vật phẩm này hiện đang có giao dịch chờ xử lý. Vui lòng thử lại sau khi giao dịch kết thúc.");
        }

        _logger.Success($"[EnsureItemCanBeListed] Item {inventoryId} đủ điều kiện tạo bài đăng");
    }

    #endregion
}