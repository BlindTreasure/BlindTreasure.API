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

    public async Task<TradeRequestDto> GetByIdAsync(Guid tradeRequestId)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
            t => t.Listing,
            t => t.Listing.InventoryItem,
            t => t.Listing.InventoryItem.Product!,
            t => t.Requester);

        if (tradeRequest == null)
            throw ErrorHelper.NotFound("Trade Request không tồn tại.");

        // Load OfferedInventory nếu có
        InventoryItem? offeredInventory = null;
        if (tradeRequest.OfferedInventoryId.HasValue)
            offeredInventory = await _unitOfWork.InventoryItems.GetByIdAsync(
                tradeRequest.OfferedInventoryId.Value,
                i => i.Product);

        return MapTradeRequestToDto(tradeRequest, offeredInventory);
    }

    public async Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId)
    {
        // Lấy thông tin listing
        var listing =
            await _unitOfWork.Listings.GetByIdAsync(listingId, l => l.InventoryItem, l => l.InventoryItem.Product!);
        if (listing == null)
            throw ErrorHelper.NotFound("Listing không tồn tại.");

        // Lấy tất cả các trade requests cho listing
        var tradeRequests = await _unitOfWork.TradeRequests.GetAllAsync(t => t.ListingId == listingId,
            t => t.OfferedInventory!, t => t.OfferedInventory.Product, t => t.Requester);

        // Map kết quả ra TradeRequestDto
        var dtos = new List<TradeRequestDto>();
        foreach (var tradeRequest in tradeRequests)
        {
            var dto = _mapper.Map<TradeRequest, TradeRequestDto>(tradeRequest);
            dto.ListingItemName = listing.InventoryItem?.Product?.Name ?? "Unknown";
            dto.RequesterName = tradeRequest.Requester.FullName ?? "Unknown";
            dto.OfferedItemName = tradeRequest.OfferedInventory?.Product?.Name;
            dtos.Add(dto);
        }

        return dtos;
    }

    public async Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, Guid? offeredInventoryId)
    {
        var userId = _claimsService.CurrentUserId;

        var listing = await _unitOfWork.Listings.GetByIdAsync(listingId, l => l.InventoryItem);
        if (listing == null || listing.Status != ListingStatus.Active)
            throw ErrorHelper.NotFound("Listing không tồn tại hoặc không còn hoạt động.");

        // Kiểm tra điều kiện item
        var offeredItem = await ValidateOfferedItem(offeredInventoryId, listing, userId);

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

        return await GetByIdAsync(tradeRequest.Id);
    }

    // 2. Respond TradeRequest (Chấp nhận hoặc từ chối)
    public async Task<TradeRequestDto> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted)
    {
        var tradeRequest =
            await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId, t => t.Listing, t => t.Listing.InventoryItem);

        if (tradeRequest == null)
            throw ErrorHelper.NotFound("Trade Request không tồn tại.");

        if (tradeRequest.Status != TradeRequestStatus.PENDING)
            throw ErrorHelper.BadRequest("Giao dịch này đã được xử lý hoặc hết hạn.");

        tradeRequest.Status = isAccepted ? TradeRequestStatus.ACCEPTED : TradeRequestStatus.REJECTED;
        tradeRequest.RespondedAt = DateTime.UtcNow;

        await UpdateInventoryItemStatusOnReject(tradeRequest, isAccepted);
        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(tradeRequestId);
    }

    // 3. Lock Deal - Tự động hoàn thành giao dịch khi cả 2 đều lock
    public async Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId)
    {
        var userId = _claimsService.CurrentUserId;
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
            t => t.Listing,
            t => t.Listing.InventoryItem,
            t => t.OfferedInventory,
            t => t.Requester);

        if (tradeRequest == null || tradeRequest.Status != TradeRequestStatus.ACCEPTED)
            throw ErrorHelper.NotFound("Trade request không tồn tại hoặc chưa được chấp nhận.");

        var listingOwnerId = tradeRequest.Listing.InventoryItem.UserId;

        // Process lock và kiểm tra hoàn thành
        await ProcessLockAndCompleteIfReady(tradeRequest, userId, listingOwnerId);

        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(tradeRequestId);
    }

    #region private methods

    private async Task<InventoryItem?> ValidateOfferedItem(Guid? offeredInventoryId, Listing listing, Guid userId)
    {
        _logger.Info(
            $"[ValidateOfferedItem] Bắt đầu validate offered item - OfferedInventoryId: {offeredInventoryId}, ListingId: {listing.Id}, UserId: {userId}");

        InventoryItem? offeredItem = null;

        if (listing.IsFree)
        {
            _logger.Info($"[ValidateOfferedItem] Listing {listing.Id} là miễn phí");

            if (offeredInventoryId.HasValue && offeredInventoryId != Guid.Empty)
            {
                _logger.Info(
                    $"[ValidateOfferedItem] Kiểm tra offered item {offeredInventoryId.Value} cho listing miễn phí");

                offeredItem = await _unitOfWork.InventoryItems.GetByIdAsync(offeredInventoryId.Value, i => i.Product);

                if (offeredItem == null)
                {
                    _logger.Error($"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} không tồn tại");
                    throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
                }

                if (offeredItem.UserId != userId)
                {
                    _logger.Error(
                        $"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} không thuộc về user {userId}, thực tế thuộc về {offeredItem.UserId}");
                    throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
                }

                if (offeredItem.Status != InventoryItemStatus.Available)
                {
                    _logger.Error(
                        $"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} có status {offeredItem.Status}, không phải Available");
                    throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
                }

                _logger.Success(
                    $"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} hợp lệ cho listing miễn phí");
            }
            else
            {
                _logger.Info($"[ValidateOfferedItem] Listing miễn phí không có offered item");
            }
        }
        else
        {
            _logger.Info($"[ValidateOfferedItem] Listing {listing.Id} yêu cầu trao đổi");

            if (!offeredInventoryId.HasValue || offeredInventoryId == Guid.Empty)
            {
                _logger.Error(
                    $"[ValidateOfferedItem] Listing {listing.Id} yêu cầu trao đổi nhưng không có offered item");
                throw ErrorHelper.BadRequest("Listing này yêu cầu bạn phải đề xuất một item để trao đổi.");
            }

            _logger.Info(
                $"[ValidateOfferedItem] Kiểm tra offered item {offeredInventoryId.Value} cho listing trao đổi");

            offeredItem = await _unitOfWork.InventoryItems.GetByIdAsync(offeredInventoryId.Value, i => i.Product);

            if (offeredItem == null)
            {
                _logger.Error($"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} không tồn tại");
                throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
            }

            if (offeredItem.UserId != userId)
            {
                _logger.Error(
                    $"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} không thuộc về user {userId}, thực tế thuộc về {offeredItem.UserId}");
                throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
            }

            if (offeredItem.Status != InventoryItemStatus.Available)
            {
                _logger.Error(
                    $"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} có status {offeredItem.Status}, không phải Available");
                throw ErrorHelper.BadRequest("Item bạn muốn đổi không hợp lệ.");
            }

            _logger.Success(
                $"[ValidateOfferedItem] Offered item {offeredInventoryId.Value} hợp lệ cho listing trao đổi");
        }

        _logger.Info(
            $"[ValidateOfferedItem] Hoàn thành validate - Offered item: {(offeredItem != null ? offeredItem.Id.ToString() : "null")}");
        return offeredItem;
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

            _logger.Info(
                $"[UpdateInventoryItemStatusOnReject] Khôi phục listing item {listingItem.Id} từ status {listingItem.Status} về Available");

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
            OfferedInventoryId = tradeRequest.OfferedInventoryId,
            FinalStatus = TradeRequestStatus.COMPLETED,
            CompletedAt = DateTime.UtcNow
        };

        _logger.Info(
            $"[CreateTradeHistory] Trade history data - ListingId: {tradeHistory.ListingId}, RequesterId: {tradeHistory.RequesterId}, OfferedInventoryId: {tradeHistory.OfferedInventoryId}");

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
            _logger.Info($"[ProcessLockAndCompleteIfReady] Owner {userId} thực hiện lock");
            tradeRequest.OwnerLocked = true;
        }
        else if (userId == tradeRequest.RequesterId)
        {
            _logger.Info($"[ProcessLockAndCompleteIfReady] Requester {userId} thực hiện lock");
            tradeRequest.RequesterLocked = true;
        }
        else
        {
            _logger.Error(
                $"[ProcessLockAndCompleteIfReady] User {userId} không có quyền lock trade request {tradeRequest.Id}");
            throw ErrorHelper.Forbidden("Bạn không có quyền lock giao dịch này.");
        }

        _logger.Info(
            $"[ProcessLockAndCompleteIfReady] Lock status sau khi update - OwnerLocked: {tradeRequest.OwnerLocked}, RequesterLocked: {tradeRequest.RequesterLocked}");

        // Khi cả 2 đã lock - tự động hoàn thành giao dịch
        if (tradeRequest.OwnerLocked && tradeRequest.RequesterLocked)
        {
            _logger.Info(
                $"[ProcessLockAndCompleteIfReady] Cả hai bên đã lock, bắt đầu hoàn thành giao dịch {tradeRequest.Id}");

            tradeRequest.LockedAt = DateTime.UtcNow;
            tradeRequest.Status = TradeRequestStatus.COMPLETED;
            tradeRequest.RespondedAt = DateTime.UtcNow;

            _logger.Info($"[ProcessLockAndCompleteIfReady] Cập nhật trade request status thành COMPLETED");

            // Chuyển quyền sở hữu listing item cho requester
            var listingItem = tradeRequest.Listing.InventoryItem;

            if (listingItem == null)
            {
                _logger.Error($"[ProcessLockAndCompleteIfReady] Listing item null cho trade request {tradeRequest.Id}");
                throw ErrorHelper.Internal("Lỗi dữ liệu listing item.");
            }

            _logger.Info(
                $"[ProcessLockAndCompleteIfReady] Chuyển listing item {listingItem.Id} từ owner {listingItem.UserId} cho requester {tradeRequest.RequesterId}");

            listingItem.UserId = tradeRequest.RequesterId;
            listingItem.Status = InventoryItemStatus.Sold;
            await _unitOfWork.InventoryItems.Update(listingItem);

            _logger.Success($"[ProcessLockAndCompleteIfReady] Đã chuyển listing item {listingItem.Id} cho requester");

            // Nếu có offered item, chuyển cho listing owner
            if (tradeRequest.OfferedInventoryId.HasValue && tradeRequest.OfferedInventory != null)
            {
                _logger.Info(
                    $"[ProcessLockAndCompleteIfReady] Chuyển offered item {tradeRequest.OfferedInventory.Id} từ requester {tradeRequest.OfferedInventory.UserId} cho owner {listingOwnerId}");

                var offeredItem = tradeRequest.OfferedInventory;
                offeredItem.UserId = listingOwnerId;
                offeredItem.Status = InventoryItemStatus.Sold;
                await _unitOfWork.InventoryItems.Update(offeredItem);

                _logger.Success(
                    $"[ProcessLockAndCompleteIfReady] Đã chuyển offered item {offeredItem.Id} cho listing owner");
            }
            else
            {
                _logger.Info($"[ProcessLockAndCompleteIfReady] Không có offered item để chuyển");
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

    private TradeRequestDto MapTradeRequestToDto(TradeRequest tradeRequest, InventoryItem? offeredItem)
    {
        var listingItemName = tradeRequest.Listing?.InventoryItem?.Product?.Name ?? "Unknown";
        var offeredItemName = offeredItem?.Product?.Name;
        var requesterName = tradeRequest.Requester?.FullName ?? "Unknown";

        var dto = new TradeRequestDto
        {
            Id = tradeRequest.Id,
            ListingId = tradeRequest.ListingId,
            ListingItemName = listingItemName,
            RequesterId = tradeRequest.RequesterId,
            RequesterName = requesterName,
            OfferedInventoryId = tradeRequest.OfferedInventoryId,
            OfferedItemName = offeredItemName,
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
}