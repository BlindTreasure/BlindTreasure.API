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

    public TradingService(IClaimsService claimsService, ILoggerService logger, IMapperService mapper,
        IUnitOfWork unitOfWork)
    {
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(
            tradeRequestId,
            t => t.Listing,
            t => t.Listing.InventoryItem
        );

        if (tradeRequest == null)
            throw ErrorHelper.NotFound("Trade Request không tồn tại.");

        // Kiểm tra trạng thái giao dịch trước khi thay đổi
        if (tradeRequest.Status != TradeRequestStatus.Pending)
            throw ErrorHelper.BadRequest("Giao dịch này đã được xử lý hoặc hết hạn.");

        // Thực hiện cập nhật trạng thái khi chấp nhận hoặc từ chối
        tradeRequest.Status = isAccepted ? TradeRequestStatus.Accepted : TradeRequestStatus.Rejected;
        tradeRequest.RespondedAt = DateTime.UtcNow;

        var listing = tradeRequest.Listing;
        var listingItem = listing.InventoryItem;

        // Nếu bị từ chối, hoàn trả trạng thái item về Available
        if (!isAccepted)
        {
            listingItem.Status = InventoryItemStatus.Available;
            await _unitOfWork.InventoryItems.Update(listingItem);
        }

        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId)
    {
        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId);
        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        var tradeRequests = await _unitOfWork.TradeRequests.GetAllAsync(t => t.ListingId == listingId,
            t => t.OfferedInventory!, t => t.Requester);
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

        // Kiểm tra điều kiện đối với item miễn phí
        if (listing.IsFree)
        {
            if (offeredInventoryId.HasValue && offeredInventoryId != Guid.Empty)
            {
                offeredItem = await _unitOfWork.InventoryItems.GetByIdAsync(offeredInventoryId.Value, i => i.Product);
                if (offeredItem == null || offeredItem.UserId != userId ||
                    offeredItem.Status != InventoryItemStatus.Available)
                    throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
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
            RequesterName = "", // Fetch the name if necessary
            OfferedInventoryId = offeredInventoryId,
            OfferedItemName = offeredItem?.Product?.Name,
            Status = tradeRequest.Status.ToString(),
            RequestedAt = tradeRequest.RequestedAt
        };
    }

    public async Task<bool> LockDealAsync(Guid tradeRequestId)
    {
        var tradeRequest =
            await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId, t => t.Listing, t => t.Listing.InventoryItem);
        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.Pending)
            throw ErrorHelper.NotFound("Trade request không tồn tại hoặc đã xử lý.");

        var listingItem = tradeRequest.Listing.InventoryItem;

        // Kiểm tra item có sẵn và chưa bị lock
        if (listingItem.Status != InventoryItemStatus.Available)
            throw ErrorHelper.BadRequest("Item không còn khả dụng.");

        // Lock item trong giao dịch
        listingItem.Status = InventoryItemStatus.Reserved;
        listingItem.LockedByRequestId = tradeRequestId;
        await _unitOfWork.InventoryItems.Update(listingItem);

        tradeRequest.Status = TradeRequestStatus.Pending; // Đảm bảo trạng thái không thay đổi khi chỉ lock
        tradeRequest.LockedAt = DateTime.UtcNow; // Cập nhật thời gian khóa
        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ConfirmDealAsync(Guid tradeRequestId)
    {
        var tradeRequest =
            await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId, t => t.Listing, t => t.Listing.InventoryItem);
        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.Pending)
            throw ErrorHelper.NotFound("Trade request không tồn tại hoặc không yêu cầu xác nhận.");

        var listingItem = tradeRequest.Listing.InventoryItem;

        // Chuyển quyền sở hữu item cho người yêu cầu
        listingItem.UserId = tradeRequest.RequesterId;
        listingItem.Status = InventoryItemStatus.Sold;
        await _unitOfWork.InventoryItems.Update(listingItem);

        tradeRequest.Status = TradeRequestStatus.Accepted;
        tradeRequest.RespondedAt = DateTime.UtcNow;

        // Tạo trade history
        var tradeHistory = new TradeHistory
        {
            ListingId = tradeRequest.ListingId,
            RequesterId = tradeRequest.RequesterId,
            OfferedInventoryId = tradeRequest.OfferedInventoryId,
            FinalStatus = TradeRequestStatus.Accepted,
            CompletedAt = DateTime.UtcNow
        };
        await _unitOfWork.TradeHistories.AddAsync(tradeHistory);

        // Cập nhật trạng thái listing
        var listing = tradeRequest.Listing;
        listing.TradeStatus = TradeStatus.Accepted;
        listing.Status = ListingStatus.Sold;
        await _unitOfWork.Listings.Update(listing);

        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ExpireDealAsync(Guid tradeRequestId)
    {
        var tradeRequest =
            await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId, t => t.Listing, t => t.Listing.InventoryItem);
        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.Pending)
            throw ErrorHelper.NotFound("Trade request không tồn tại hoặc không hết hạn.");

        tradeRequest.Status = TradeRequestStatus.Expired;

        var listingItem = tradeRequest.Listing.InventoryItem;
        listingItem.Status = InventoryItemStatus.Available;
        listingItem.LockedByRequestId = null;
        await _unitOfWork.InventoryItems.Update(listingItem);

        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}