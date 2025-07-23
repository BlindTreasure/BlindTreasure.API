using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class TradingService : ITradingService
{
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public TradingService(IClaimsService claimsService, ILoggerService logger, IMapperService mapper, IUnitOfWork unitOfWork)
    {
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId)
    {
        // Lấy thông tin listing
        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId);
        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        // Lấy tất cả các trade requests cho listing
        var tradeRequests = await _unitOfWork.TradeRequests.GetAllAsync(t => t.ListingId == listingId,
            t => t.OfferedInventory!, t => t.Requester);
    
        // Map kết quả ra TradeRequestDto
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

    public async Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, Guid? offeredInventoryId)
    {
        var userId = _claimsService.CurrentUserId;

        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId, l => l.InventoryItem);
        if (listing == null || listing.Status != ListingStatus.Active)
            throw ErrorHelper.NotFound("Listing không tồn tại hoặc không còn hoạt động.");

        InventoryItem? offeredItem = null;
        
        // Kiểm tra điều kiện item
        offeredItem = await ValidateOfferedItem(offeredInventoryId, listing, userId);

        var tradeRequest = new TradeRequest
        {
            ListingId = listingId,
            RequesterId = userId,
            OfferedInventoryId = offeredInventoryId,
            Status = TradeRequestStatus.PENDING,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.TradeRequests.AddAsync(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return MapTradeRequestToDto(tradeRequest, offeredItem);
    }

    // 2. Respond TradeRequest (Chấp nhận hoặc từ chối)
    public async Task<bool> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId, t => t.Listing, t => t.Listing.InventoryItem);

        if (tradeRequest == null)
            throw ErrorHelper.NotFound("Trade Request không tồn tại.");

        if (tradeRequest.Status != TradeRequestStatus.PENDING)
            throw ErrorHelper.BadRequest("Giao dịch này đã được xử lý hoặc hết hạn.");

        tradeRequest.Status = isAccepted ? TradeRequestStatus.ACCEPTED : TradeRequestStatus.REJECTED;
        tradeRequest.RespondedAt = DateTime.UtcNow;

        await UpdateInventoryItemStatusOnReject(tradeRequest, isAccepted);
        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    // 3. Lock Deal - Tự động hoàn thành giao dịch khi cả 2 đều lock
    public async Task<bool> LockDealAsync(Guid tradeRequestId)
    {
        var userId = _claimsService.CurrentUserId;
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId, 
            t => t.Listing, 
            t => t.Listing.InventoryItem,
            t => t.OfferedInventory);

        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.ACCEPTED)
            throw ErrorHelper.NotFound("Trade request không tồn tại hoặc chưa được chấp nhận.");

        var listingOwnerId = tradeRequest.Listing.InventoryItem.UserId;

        // Xác định user nào lock
        if (userId == listingOwnerId)
            tradeRequest.OwnerLocked = true;
        else if (userId == tradeRequest.RequesterId)
            tradeRequest.RequesterLocked = true;
        else
            throw ErrorHelper.Forbidden("Bạn không có quyền lock giao dịch này.");

        // Khi cả 2 đã lock - tự động hoàn thành giao dịch
        if (tradeRequest.OwnerLocked && tradeRequest.RequesterLocked)
        {
            tradeRequest.LockedAt = DateTime.UtcNow;
            tradeRequest.Status = TradeRequestStatus.COMPLETED;
            tradeRequest.RespondedAt = DateTime.UtcNow;

            // Chuyển quyền sở hữu listing item cho requester
            var listingItem = tradeRequest.Listing.InventoryItem;
            listingItem.UserId = tradeRequest.RequesterId;
            listingItem.Status = InventoryItemStatus.Sold;
            await _unitOfWork.InventoryItems.Update(listingItem);

            // Nếu có offered item, chuyển cho listing owner
            if (tradeRequest.OfferedInventoryId.HasValue && tradeRequest.OfferedInventory != null)
            {
                var offeredItem = tradeRequest.OfferedInventory;
                offeredItem.UserId = listingOwnerId;
                offeredItem.Status = InventoryItemStatus.Sold;
                await _unitOfWork.InventoryItems.Update(offeredItem);
            }

            // Tạo trade history
            await CreateTradeHistory(tradeRequest);

            // Cập nhật listing status
            var listing = tradeRequest.Listing;
            listing.TradeStatus = TradeStatus.Accepted;
            listing.Status = ListingStatus.Sold;
            await _unitOfWork.Listings.Update(listing);
        }

        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    // 4. Expire Deal (Khi không có hành động nào từ 2 bên)
    public async Task<bool> ExpireDealAsync(Guid tradeRequestId)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId, t => t.Listing, t => t.Listing.InventoryItem);
        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.PENDING)
            throw ErrorHelper.NotFound("Trade request không tồn tại hoặc không hết hạn.");

        tradeRequest.Status = TradeRequestStatus.EXPIRED;
        var listingItem = tradeRequest.Listing.InventoryItem;
        listingItem.Status = InventoryItemStatus.Available;
        listingItem.LockedByRequestId = null;

        await _unitOfWork.InventoryItems.Update(listingItem);
        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    // Helper methods
    private async Task<InventoryItem?> ValidateOfferedItem(Guid? offeredInventoryId, Listing listing, Guid userId)
    {
        InventoryItem? offeredItem = null;

        if (listing.IsFree)
        {
            if (offeredInventoryId.HasValue && offeredInventoryId != Guid.Empty)
            {
                offeredItem = await _unitOfWork.InventoryItems.GetByIdAsync(offeredInventoryId.Value, i => i.Product);
                if (offeredItem == null || offeredItem.UserId != userId || offeredItem.Status != InventoryItemStatus.Available)
                    throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
            }
        }
        else
        {
            if (!offeredInventoryId.HasValue || offeredInventoryId == Guid.Empty)
                throw ErrorHelper.BadRequest("Listing này yêu cầu bạn phải đề xuất một item để trao đổi.");

            offeredItem = await _unitOfWork.InventoryItems.GetByIdAsync(offeredInventoryId.Value, i => i.Product);
            if (offeredItem == null || offeredItem.UserId != userId || offeredItem.Status != InventoryItemStatus.Available)
                throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
        }
        return offeredItem;
    }

    private async Task UpdateInventoryItemStatusOnReject(TradeRequest tradeRequest, bool isAccepted)
    {
        if (!isAccepted)
        {
            var listingItem = tradeRequest.Listing.InventoryItem;
            listingItem.Status = InventoryItemStatus.Available;
            await _unitOfWork.InventoryItems.Update(listingItem);
        }
    }

    private async Task CreateTradeHistory(TradeRequest tradeRequest)
    {
        var tradeHistory = new TradeHistory
        {
            ListingId = tradeRequest.ListingId,
            RequesterId = tradeRequest.RequesterId,
            OfferedInventoryId = tradeRequest.OfferedInventoryId,
            FinalStatus = TradeRequestStatus.COMPLETED,
            CompletedAt = DateTime.UtcNow
        };
        await _unitOfWork.TradeHistories.AddAsync(tradeHistory);
    }

    private TradeRequestDto MapTradeRequestToDto(TradeRequest tradeRequest, InventoryItem? offeredItem)
    {
        return new TradeRequestDto
        {
            Id = tradeRequest.Id,
            ListingId = tradeRequest.Listing.Id,
            ListingItemName = tradeRequest.Listing.InventoryItem?.Product?.Name ?? "Unknown",
            RequesterId = tradeRequest.RequesterId,
            OfferedInventoryId = tradeRequest.OfferedInventoryId,
            OfferedItemName = offeredItem?.Product?.Name,
            Status = tradeRequest.Status.ToString(),
            RequestedAt = tradeRequest.RequestedAt
        };
    }
}