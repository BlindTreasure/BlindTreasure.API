using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain;
using BlindTreasure.Domain.DTOs.TradeHistoryDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class TradingService : ITradingService
{
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;

    public TradingService(IClaimsService claimsService, ILoggerService logger,
        IUnitOfWork unitOfWork)
    {
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<TradeRequestDto> GetTradeRequestByIdAsync(Guid tradeRequestId)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
            t => t.Listing!,
            t => t.Listing!.InventoryItem,
            t => t.Listing!.InventoryItem.Product!,
            t => t.Requester!,
            t => t.OfferedItems);

        if (tradeRequest == null)
            throw ErrorHelper.NotFound("Trade Request không tồn tại.");

        // Load chi tiết các offered items
        var offeredInventoryItems = new List<InventoryItem>();
        if (tradeRequest.OfferedItems.Any())
        {
            var itemIds = tradeRequest.OfferedItems.Select(oi => oi.InventoryItemId).ToList();
            offeredInventoryItems = await _unitOfWork.InventoryItems.GetAllAsync(
                i => itemIds.Contains(i.Id),
                i => i.Product!);
        }

        return MapTradeRequestToDto(tradeRequest, offeredInventoryItems);
    }

    public async Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId)
    {
        // Lấy thông tin listing
        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId,
            l => l.InventoryItem,
            l => l.InventoryItem.Product!);

        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        // Lấy tất cả các trade requests cho listing
        var tradeRequests = await _unitOfWork.TradeRequests.GetAllAsync(
            t => t.ListingId == listingId,
            t => t.Requester!,
            t => t.OfferedItems);

        var dtos = new List<TradeRequestDto>();
        foreach (var tradeRequest in tradeRequests)
        {
            // Load offered items cho mỗi trade request
            var offeredInventoryItems = new List<InventoryItem>();
            if (tradeRequest.OfferedItems.Any())
            {
                var itemIds = tradeRequest.OfferedItems.Select(oi => oi.InventoryItemId).ToList();
                offeredInventoryItems = await _unitOfWork.InventoryItems.GetAllAsync(
                    i => itemIds.Contains(i.Id),
                    i => i.Product);
            }

            var dto = MapTradeRequestToDto(tradeRequest, offeredInventoryItems);
            dto.ListingItemName = listing.InventoryItem?.Product?.Name ?? "Unknown";
            dtos.Add(dto);
        }

        return dtos;
    }

    public async Task<Pagination<TradeHistoryDto>> GetAllTradeHistoriesAsync(TradeHistoryQueryParameter param)
    {
        _logger.Info($"[GetAllTradeHistoriesAsync] Page: {param.PageIndex}, Size: {param.PageSize}, " +
                     $"Status: {param.FinalStatus}, RequesterId: {param.RequesterId}, Desc: {param.Desc}");

        var query = _unitOfWork.TradeHistories.GetQueryable()
            .Include(th => th.Listing)
            .ThenInclude(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(th => th.Requester)
            .Include(th => th.OfferedInventory)
            .ThenInclude(oi => oi!.Product)
            .Where(th => !th.IsDeleted)
            .AsNoTracking();

        // Apply filters
        query = ApplyTradeHistoryFilters(query, param);

        // Apply sorting
        query = ApplyTradeHistorySorting(query, param);

        var count = await query.CountAsync();

        var tradeHistories = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var tradeHistoryDtos = tradeHistories.Select(MapTradeHistoryToDto).ToList();

        return new Pagination<TradeHistoryDto>(tradeHistoryDtos, count, param.PageIndex, param.PageSize);
    }


    public async Task<TradeRequestDto> CreateTradeRequestAsync(CreateTradeRequestDto request)
    {
        var userId = _claimsService.CurrentUserId;

        var listing = await _unitOfWork.Listings.GetByIdAsync(request.ListingId, l => l.InventoryItem);
        if (listing == null || listing.Status != ListingStatus.Active)
            throw ErrorHelper.NotFound("Listing không tồn tại hoặc không còn hoạt động.");

        // Validate multiple offered items
        await ValidateMultipleOfferedItems(request.OfferedInventoryIds, listing, userId);

        var tradeRequest = new TradeRequest
        {
            ListingId = request.ListingId,
            RequesterId = userId,
            Status = TradeRequestStatus.PENDING,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.TradeRequests.AddAsync(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        // Tạo TradeRequestItems
        if (request.OfferedInventoryIds.Any())
        {
            var tradeRequestItems = request.OfferedInventoryIds.Select(itemId => new TradeRequestItem
            {
                TradeRequestId = tradeRequest.Id,
                InventoryItemId = itemId
            }).ToList();

            await _unitOfWork.TradeRequestItems.AddRangeAsync(tradeRequestItems);
            await _unitOfWork.SaveChangesAsync();
        }

        return await GetTradeRequestByIdAsync(tradeRequest.Id);
    }

    public async Task<TradeRequestDto> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
            t => t.Listing!,
            t => t.Listing!.InventoryItem);

        if (tradeRequest == null)
            throw ErrorHelper.NotFound("Trade Request không tồn tại.");

        if (tradeRequest.Status != TradeRequestStatus.PENDING)
            throw ErrorHelper.BadRequest("Giao dịch này đã được xử lý hoặc hết hạn.");

        tradeRequest.Status = isAccepted ? TradeRequestStatus.ACCEPTED : TradeRequestStatus.REJECTED;
        tradeRequest.RespondedAt = DateTime.UtcNow;

        await UpdateInventoryItemStatusOnReject(tradeRequest, isAccepted);
        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return await GetTradeRequestByIdAsync(tradeRequestId);
    }

    public async Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId)
    {
        var userId = _claimsService.CurrentUserId;
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
            t => t.Listing!,
            t => t.Listing!.InventoryItem,
            t => t.OfferedItems,
            t => t.Requester!);

        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.ACCEPTED)
            throw ErrorHelper.NotFound("Trade request không tồn tại hoặc chưa được chấp nhận.");

        var listingOwnerId = tradeRequest.Listing!.InventoryItem.UserId; // User A

        // Process lock và kiểm tra hoàn thành
        await ProcessLockAndCompleteIfReady(tradeRequest, userId, listingOwnerId);

        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return await GetTradeRequestByIdAsync(tradeRequestId);
    }

    #region Private Methods

    private async Task<List<InventoryItem>> ValidateMultipleOfferedItems(List<Guid> offeredInventoryIds,
        Listing listing, Guid userId)
    {
        _logger.Info(
            $"[ValidateMultipleOfferedItems] Bắt đầu validate {offeredInventoryIds.Count} offered items cho user {userId}");

        var offeredItems = new List<InventoryItem>();

        if (listing.IsFree)
        {
            _logger.Info($"[ValidateMultipleOfferedItems] Listing {listing.Id} là miễn phí");

            if (offeredInventoryIds.Any())
            {
                _logger.Info(
                    $"[ValidateMultipleOfferedItems] Kiểm tra {offeredInventoryIds.Count} items cho listing miễn phí");
                offeredItems = await ValidateItemsOwnership(offeredInventoryIds, userId);
            }
        }
        else
        {
            _logger.Info($"[ValidateMultipleOfferedItems] Listing {listing.Id} yêu cầu trao đổi");

            if (!offeredInventoryIds.Any())
                throw ErrorHelper.BadRequest("Listing này yêu cầu bạn phải đề xuất ít nhất một item để trao đổi.");

            offeredItems = await ValidateItemsOwnership(offeredInventoryIds, userId);
        }

        _logger.Success($"[ValidateMultipleOfferedItems] Validate thành công {offeredItems.Count} items");
        return offeredItems;
    }

    private async Task<List<InventoryItem>> ValidateItemsOwnership(List<Guid> itemIds, Guid userId)
    {
        var items = await _unitOfWork.InventoryItems.GetAllAsync(
            i => itemIds.Contains(i.Id),
            i => i.Product);

        if (items.Count != itemIds.Count)
            throw ErrorHelper.BadRequest("Một số item bạn muốn đổi không hợp lệ.");

        foreach (var item in items)
        {
            if (item.UserId != userId)
                throw ErrorHelper.BadRequest($"Item '{item.Product?.Name}' không thuộc về bạn.");

            if (item.Status != InventoryItemStatus.Available)
                throw ErrorHelper.BadRequest($"Item '{item.Product?.Name}' không khả dụng.");
        }

        return items;
    }

    private async Task UpdateInventoryItemStatusOnReject(TradeRequest tradeRequest, bool isAccepted)
    {
        _logger.Info(
            $"[UpdateInventoryItemStatusOnReject] Bắt đầu xử lý reject status - TradeRequestId: {tradeRequest.Id}, IsAccepted: {isAccepted}");

        if (!isAccepted)
        {
            _logger.Info(
                $"[UpdateInventoryItemStatusOnReject] Trade request {tradeRequest.Id} bị reject, khôi phục status listing item");

            var listingItem = tradeRequest.Listing.InventoryItem;
            if (listingItem == null)
            {
                _logger.Error(
                    $"[UpdateInventoryItemStatusOnReject] Listing item null cho trade request {tradeRequest.Id}");
                return;
            }

            listingItem.Status = InventoryItemStatus.Available;
            await _unitOfWork.InventoryItems.Update(listingItem);
            _logger.Success(
                $"[UpdateInventoryItemStatusOnReject] Đã khôi phục listing item {listingItem.Id} về status Available");
        }
        else
        {
            _logger.Info(
                $"[UpdateInventoryItemStatusOnReject] Trade request {tradeRequest.Id} được accept, không cần khôi phục status");
        }
    }

    private async Task CreateTradeHistory(TradeRequest tradeRequest)
    {
        _logger.Info($"[CreateTradeHistory] Tạo trade history cho trade request {tradeRequest.Id}");

        var tradeHistory = new TradeHistory
        {
            ListingId = tradeRequest.ListingId,
            RequesterId = tradeRequest.RequesterId,
            OfferedInventoryId = tradeRequest.OfferedItems.FirstOrDefault()?.InventoryItemId, // Legacy support
            FinalStatus = TradeRequestStatus.COMPLETED,
            CompletedAt = DateTime.UtcNow
        };

        await _unitOfWork.TradeHistories.AddAsync(tradeHistory);
        _logger.Success(
            $"[CreateTradeHistory] Đã tạo trade history {tradeHistory.Id} cho trade request {tradeRequest.Id}");
    }

    private async Task ProcessLockAndCompleteIfReady(TradeRequest tradeRequest, Guid userId, Guid listingOwnerId)
    {
        _logger.Info(
            $"[ProcessLockAndCompleteIfReady] Xử lý lock - TradeRequestId: {tradeRequest.Id}, UserId: {userId}, ListingOwnerId: {listingOwnerId}");
        _logger.Info(
            $"[ProcessLockAndCompleteIfReady] Current lock status - OwnerLocked: {tradeRequest.OwnerLocked}, RequesterLocked: {tradeRequest.RequesterLocked}");

        // Xác định user nào lock
        if (userId == listingOwnerId)
        {
            _logger.Info($"[ProcessLockAndCompleteIfReady] Owner (User A) {userId} thực hiện lock");
            tradeRequest.OwnerLocked = true;
        }
        else if (userId == tradeRequest.RequesterId)
        {
            _logger.Info($"[ProcessLockAndCompleteIfReady] Requester (User B) {userId} thực hiện lock");
            tradeRequest.RequesterLocked = true;
        }
        else
        {
            _logger.Error(
                $"[ProcessLockAndCompleteIfReady] User {userId} không có quyền lock trade request {tradeRequest.Id}");
            throw ErrorHelper.Forbidden("Bạn không có quyền lock giao dịch này.");
        }

        // Khi cả 2 đã lock - tự động hoàn thành giao dịch
        if (tradeRequest.OwnerLocked && tradeRequest.RequesterLocked)
        {
            _logger.Info(
                $"[ProcessLockAndCompleteIfReady] Cả hai bên đã lock, bắt đầu hoàn thành giao dịch {tradeRequest.Id}");

            tradeRequest.LockedAt = DateTime.UtcNow;
            tradeRequest.Status = TradeRequestStatus.COMPLETED;
            tradeRequest.RespondedAt = DateTime.UtcNow;

            // Chuyển listing item từ User A cho User B
            var listingItem = tradeRequest.Listing.InventoryItem;
            if (listingItem == null)
            {
                _logger.Error($"[ProcessLockAndCompleteIfReady] Listing item null cho trade request {tradeRequest.Id}");
                throw ErrorHelper.Internal("Lỗi dữ liệu listing item.");
            }

            _logger.Info(
                $"[ProcessLockAndCompleteIfReady] Chuyển listing item {listingItem.Id} từ owner {listingItem.UserId} cho requester {tradeRequest.RequesterId}");
            listingItem.UserId = tradeRequest.RequesterId; // User B nhận được listing item
            listingItem.Status = InventoryItemStatus.Sold;
            await _unitOfWork.InventoryItems.Update(listingItem);
            _logger.Success($"[ProcessLockAndCompleteIfReady] Đã chuyển listing item {listingItem.Id} cho requester");

            // Chuyển offered items từ User B cho User A
            if (tradeRequest.OfferedItems.Any())
            {
                var offeredItemIds = tradeRequest.OfferedItems.Select(oi => oi.InventoryItemId).ToList();
                var offeredInventoryItems =
                    await _unitOfWork.InventoryItems.GetAllAsync(i => offeredItemIds.Contains(i.Id));

                foreach (var item in offeredInventoryItems)
                {
                    _logger.Info(
                        $"[ProcessLockAndCompleteIfReady] Chuyển offered item {item.Id} từ requester {item.UserId} cho owner {listingOwnerId}");
                    item.UserId = listingOwnerId; // User A nhận được offered items
                    item.Status = InventoryItemStatus.Sold;
                    await _unitOfWork.InventoryItems.Update(item);
                }

                _logger.Success(
                    $"[ProcessLockAndCompleteIfReady] Đã chuyển {offeredInventoryItems.Count} offered items cho listing owner");
            }
            else
            {
                _logger.Info($"[ProcessLockAndCompleteIfReady] Không có offered items để chuyển");
            }

            // Tạo trade history
            _logger.Info($"[ProcessLockAndCompleteIfReady] Tạo trade history cho giao dịch hoàn thành");
            await CreateTradeHistory(tradeRequest);

            // Cập nhật listing status
            var listing = tradeRequest.Listing;
            _logger.Info(
                $"[ProcessLockAndCompleteIfReady] Cập nhật listing {listing.Id} status từ {listing.Status} và trade status từ {listing.TradeStatus} thành COMPLETED/Sold");
            listing.TradeStatus = TradeStatus.COMPLETED;
            listing.Status = ListingStatus.Sold;
            await _unitOfWork.Listings.Update(listing);

            _logger.Success($"[ProcessLockAndCompleteIfReady] Hoàn thành giao dịch {tradeRequest.Id} thành công");
        }
        else
        {
            _logger.Info($"[ProcessLockAndCompleteIfReady] Chưa đủ điều kiện hoàn thành (cần cả hai bên lock)");
        }
    }

    private TradeRequestDto MapTradeRequestToDto(TradeRequest tradeRequest, List<InventoryItem> offeredItems)
    {
        var listingItemName = tradeRequest.Listing?.InventoryItem?.Product?.Name ?? "Unknown";
        var requesterName = tradeRequest.Requester?.FullName ?? "Unknown";

        var offeredItemDtos = offeredItems.Select(item => new OfferedItemDto
        {
            InventoryItemId = item.Id,
            ItemName = item.Product?.Name,
            ImageUrl = item.Product?.ImageUrls?.FirstOrDefault()
        }).ToList();

        var dto = new TradeRequestDto
        {
            Id = tradeRequest.Id,
            ListingId = tradeRequest.ListingId,
            ListingItemName = listingItemName,
            RequesterId = tradeRequest.RequesterId,
            RequesterName = requesterName,
            OfferedItems = offeredItemDtos,
            Status = tradeRequest.Status,
            RequestedAt = tradeRequest.RequestedAt,
            RespondedAt = tradeRequest.RespondedAt,
            OwnerLocked = tradeRequest.OwnerLocked,
            RequesterLocked = tradeRequest.RequesterLocked,
            LockedAt = tradeRequest.LockedAt
        };

        return dto;
    }

    #endregion

    #region Private Methods for TradeHistory

    private IQueryable<TradeHistory> ApplyTradeHistoryFilters(IQueryable<TradeHistory> query,
        TradeHistoryQueryParameter param)
    {
        // Filter by FinalStatus
        if (param.FinalStatus.HasValue) query = query.Where(th => th.FinalStatus == param.FinalStatus.Value);

        // Filter by RequesterId
        if (param.RequesterId.HasValue) query = query.Where(th => th.RequesterId == param.RequesterId.Value);

        // Filter by ListingId
        if (param.ListingId.HasValue) query = query.Where(th => th.ListingId == param.ListingId.Value);

        // Filter by CompletedAt date range
        if (param.CompletedFromDate.HasValue)
            query = query.Where(th => th.CompletedAt >= param.CompletedFromDate.Value);

        if (param.CompletedToDate.HasValue) query = query.Where(th => th.CompletedAt <= param.CompletedToDate.Value);

        // Filter by CreatedAt date range
        if (param.CreatedFromDate.HasValue) query = query.Where(th => th.CreatedAt >= param.CreatedFromDate.Value);

        if (param.CreatedToDate.HasValue) query = query.Where(th => th.CreatedAt <= param.CreatedToDate.Value);

        return query;
    }

    private IQueryable<TradeHistory> ApplyTradeHistorySorting(IQueryable<TradeHistory> query,
        TradeHistoryQueryParameter param)
    {
        var sortBy = param.SortBy?.ToLower() ?? "completedat";
        var isDescending = param.Desc;

        query = sortBy switch
        {
            "completedat" => isDescending
                ? query.OrderByDescending(th => th.CompletedAt)
                : query.OrderBy(th => th.CompletedAt),

            "createdat" => isDescending
                ? query.OrderByDescending(th => th.CreatedAt)
                : query.OrderBy(th => th.CreatedAt),

            "finalstatus" => isDescending
                ? query.OrderByDescending(th => th.FinalStatus)
                : query.OrderBy(th => th.FinalStatus),

            "requesterid" => isDescending
                ? query.OrderByDescending(th => th.RequesterId)
                : query.OrderBy(th => th.RequesterId),

            "listingid" => isDescending
                ? query.OrderByDescending(th => th.ListingId)
                : query.OrderBy(th => th.ListingId),

            "requestername" => isDescending
                ? query.OrderByDescending(th => th.Requester.FullName ?? th.Requester.FullName)
                : query.OrderBy(th => th.Requester.FullName ?? th.Requester.FullName),

            "listingitemname" => isDescending
                ? query.OrderByDescending(th => th.Listing.InventoryItem.Product!.Name)
                : query.OrderBy(th => th.Listing.InventoryItem.Product!.Name),

            _ => isDescending
                ? query.OrderByDescending(th => th.CompletedAt) // Default sort
                : query.OrderBy(th => th.CompletedAt)
        };

        return query;
    }

    private TradeHistoryDto MapTradeHistoryToDto(TradeHistory tradeHistory)
    {
        var dto = new TradeHistoryDto
        {
            Id = tradeHistory.Id,
            ListingId = tradeHistory.ListingId,
            ListingItemName = tradeHistory.Listing.InventoryItem?.Product?.Name ?? "Unknown",
            ListingItemImage = tradeHistory.Listing?.InventoryItem?.Product?.ImageUrls?.FirstOrDefault() ?? "",
            RequesterId = tradeHistory.RequesterId,
            RequesterName = tradeHistory.Requester.FullName ?? tradeHistory.Requester?.FullName ?? "Unknown",
            OfferedInventoryId = tradeHistory.OfferedInventoryId,
            OfferedItemName = tradeHistory.OfferedInventory?.Product?.Name ?? "No Item",
            OfferedItemImage = tradeHistory.OfferedInventory?.Product?.ImageUrls?.FirstOrDefault() ?? "",
            FinalStatus = tradeHistory.FinalStatus,
            CompletedAt = tradeHistory.CompletedAt,
            CreatedAt = tradeHistory.CreatedAt
        };

        return dto;
    }

    #endregion
}