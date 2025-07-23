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

    #region private methods

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