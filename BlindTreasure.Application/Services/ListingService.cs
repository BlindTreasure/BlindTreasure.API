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
            .Select(l => {
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

        return items.Select(item => {
            var dto = _mapper.Map<InventoryItem, InventoryItemDto>(item);
            return dto;
        }).ToList();
    }

    public async Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId)
    {
        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId);
        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        var tradeRequests = await _unitOfWork.TradeRequests.GetAllAsync(t => t.ListingId == listingId,
            t => t.OfferedInventory, t => t.Requester);
        return tradeRequests.Select(t => new TradeRequestDto
        {
            Id = t.Id,
            ListingId = t.ListingId,
            ListingItemName = listing.InventoryItem?.Product?.Name ?? "Unknown",
            RequesterId = t.RequesterId,
            RequesterName = t.Requester.FullName ?? "Unknown",
            OfferedInventoryId = t.OfferedInventoryId,
            OfferedItemName = t.OfferedInventory?.Product?.Name,
            Status = t.Status.ToString(),
            RequestedAt = t.RequestedAt,
            RespondedAt = t.RespondedAt
        }).ToList();
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
            IsFree = dto.IsFree,
            DesiredItemId = dto.IsFree ? null : dto.DesiredItemId,
            DesiredItemName = dto.IsFree ? null : dto.DesiredItemName,
            Description = dto.Description,
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

    public async Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, Guid? offeredInventoryId)
    {
        var userId = _claimsService.CurrentUserId;

        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId, l => l.InventoryItem);
        if (listing == null || listing.Status != ListingStatus.Active)
            throw ErrorHelper.NotFound("Listing không tồn tại hoặc không còn hoạt động.");

        InventoryItem? offeredItem = null;

        // Listing là free => không cần offeredInventoryId
        if (listing.IsFree)
        {
            if (offeredInventoryId.HasValue && offeredInventoryId != Guid.Empty)
            {
                offeredItem = await _unitOfWork.InventoryItems.GetByIdAsync(offeredInventoryId.Value, i => i.Product);
                if (offeredItem == null || offeredItem.UserId != userId ||
                    offeredItem.Status != InventoryItemStatus.Available)
                    throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
            }
            else
            {
                offeredInventoryId = null;
            }
        }
        else
        {
            if (!offeredInventoryId.HasValue || offeredInventoryId == Guid.Empty)
                throw ErrorHelper.BadRequest("Listing này yêu cầu bạn phải đề xuất một item để trao đổi.");

            offeredItem = await _unitOfWork.InventoryItems.GetByIdAsync(offeredInventoryId.Value, i => i.Product);
            if (offeredItem == null || offeredItem.UserId != userId ||
                offeredItem.Status != InventoryItemStatus.Available)
                throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
        }

        var tradeRequest = new TradeRequest
        {
            ListingId = listingId,
            RequesterId = userId,
            OfferedInventoryId = offeredInventoryId,
            Status = TradeRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.TradeRequests.AddAsync(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return new TradeRequestDto
        {
            Id = tradeRequest.Id,
            ListingId = listing.Id,
            ListingItemName = listing.InventoryItem?.Product?.Name ?? "Unknown",
            RequesterId = userId,
            RequesterName = "",
            OfferedInventoryId = offeredInventoryId,
            OfferedItemName = offeredItem?.Product?.Name,
            Status = tradeRequest.Status.ToString(),
            RequestedAt = tradeRequest.RequestedAt
        };
    }

    public async Task<bool> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(
            tradeRequestId,
            t => t.Listing,
            t => t.Listing.InventoryItem
        );

        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.Pending)
            throw ErrorHelper.NotFound("Trade Request không tồn tại hoặc đã xử lý.");

        var listing = tradeRequest.Listing;
        var listingItem = listing.InventoryItem;
        if (listingItem == null)
            throw ErrorHelper.NotFound("Không tìm thấy item trong listing.");

        var listingOwnerId = listingItem.UserId;
        if (listingOwnerId != _claimsService.CurrentUserId)
            throw ErrorHelper.Forbidden("Bạn không có quyền phản hồi trade request này.");

        tradeRequest.Status = isAccepted ? TradeRequestStatus.Accepted : TradeRequestStatus.Rejected;
        tradeRequest.RespondedAt = DateTime.UtcNow;

        if (isAccepted)
        {
            // --- Nếu listing là free ---
            if (listing.IsFree)
            {
                // Chuyển item trong listing cho requester
                listingItem.UserId = tradeRequest.RequesterId;
                listingItem.Status = InventoryItemStatus.Available;
                await _unitOfWork.InventoryItems.Update(listingItem);
            }
            else
            {
                // --- Listing dạng trade ---
                // Chuyển listing item cho requester
                listingItem.UserId = tradeRequest.RequesterId;
                listingItem.Status = InventoryItemStatus.Available;
                await _unitOfWork.InventoryItems.Update(listingItem);

                // Nếu requester offer item (2 chiều) -> chuyển cho owner
                if (tradeRequest.OfferedInventoryId.HasValue)
                {
                    var offeredItem =
                        await _unitOfWork.InventoryItems.GetByIdAsync(tradeRequest.OfferedInventoryId.Value);
                    if (offeredItem == null)
                        throw ErrorHelper.NotFound("Item đề xuất trao đổi không tồn tại.");

                    offeredItem.UserId = listingOwnerId;
                    offeredItem.Status = InventoryItemStatus.Available;
                    await _unitOfWork.InventoryItems.Update(offeredItem);
                }
            }

            // Log trade history
            var tradeHistory = new TradeHistory
            {
                ListingId = tradeRequest.ListingId,
                RequesterId = tradeRequest.RequesterId,
                OfferedInventoryId = tradeRequest.OfferedInventoryId,
                FinalStatus = TradeRequestStatus.Accepted,
                CompletedAt = DateTime.UtcNow
            };
            await _unitOfWork.TradeHistories.AddAsync(tradeHistory);

            // Update trạng thái listing
            listing.TradeStatus = TradeStatus.Accepted;
            listing.Status = ListingStatus.Sold;
            await _unitOfWork.Listings.Update(listing);
        }

        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}