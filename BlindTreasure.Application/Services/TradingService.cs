using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.TradeHistoryDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class TradingService : ITradingService
{
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IListingService _listingService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<NotificationHub> _notificationHub;

    public TradingService(IClaimsService claimsService, ILoggerService logger,
        IUnitOfWork unitOfWork, INotificationService notificationService,
        IListingService listingService, IHubContext<NotificationHub> notificationHub)
    {
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _listingService = listingService;
        _notificationHub = notificationHub;
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

    public async Task<Pagination<TradeHistoryDto>> GetTradeHistoriesAsync(TradeHistoryQueryParameter param,
        bool onlyMine = false)
    {
        var userId = _claimsService.CurrentUserId;

        // Ghi log với thông tin phù hợp dựa vào chế độ truy vấn
        if (onlyMine)
            _logger.Info(
                $"[GetTradeHistoriesAsync] Lấy trade histories của user {userId}, Page: {param.PageIndex}, Size: {param.PageSize}");
        else
            _logger.Info(
                $"[GetTradeHistoriesAsync] Lấy tất cả trade histories, Page: {param.PageIndex}, Size: {param.PageSize}, " +
                $"Status: {param.FinalStatus}, RequesterId: {param.RequesterId}, Desc: {param.Desc}");

        var query = _unitOfWork.TradeHistories.GetQueryable()
            .Include(th => th.Listing)
            .ThenInclude(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(th => th.Requester)
            .Include(th => th.OfferedInventory)
            .ThenInclude(oi => oi!.Product)
            .Where(th => !th.IsDeleted);

        if (onlyMine)
        {
            query = query.Where(th => th.RequesterId == userId);

            // Tạo param mới bỏ qua RequesterId vì đã lọc ở trên
            param = new TradeHistoryQueryParameter
            {
                FinalStatus = param.FinalStatus,
                ListingId = param.ListingId,
                CompletedFromDate = param.CompletedFromDate,
                CompletedToDate = param.CompletedToDate,
                CreatedFromDate = param.CreatedFromDate,
                CreatedToDate = param.CreatedToDate,
                SortBy = param.SortBy,
                Desc = param.Desc,
                PageIndex = param.PageIndex,
                PageSize = param.PageSize
            };
        }

        query = query.AsNoTracking();

        // Áp dụng các bộ lọc và sắp xếp thống nhất
        query = ApplyTradeHistoryFilters(query, param);
        query = ApplyTradeHistorySorting(query, param);

        // Đếm tổng số bản ghi
        var count = await query.CountAsync();

        // Phân trang dữ liệu
        var tradeHistories = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        // Chuyển đổi sang DTO
        var tradeHistoryDtos = tradeHistories.Select(MapTradeHistoryToDto).ToList();

        // Ghi log thành công nếu là "chỉ của tôi"
        if (onlyMine) _logger.Success($"[GetTradeHistoriesAsync] Tìm thấy {count} trade histories của user {userId}");

        return new Pagination<TradeHistoryDto>(tradeHistoryDtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, CreateTradeRequestDto request)
    {
        var userId = _claimsService.CurrentUserId;

        var listing = await ValidateTradeRequestCreation(listingId, userId);

        await ValidateMultipleOfferedItems(request.OfferedInventoryIds, listing, userId);

        var tradeRequest = new TradeRequest
        {
            ListingId = listingId,
            RequesterId = userId,
            Status = TradeRequestStatus.PENDING,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.TradeRequests.AddAsync(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

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

        try
        {
            var listingOwner = await _unitOfWork.Users.GetByIdAsync(listing.InventoryItem.UserId);
            var requester = await _unitOfWork.Users.GetByIdAsync(userId);

            if (listingOwner != null && requester != null)
                await SendTradeRequestNotificationIfNotSentAsync(listingOwner, requester.FullName);
        }
        catch (Exception ex)
        {
            _logger.Error($"[CreateTradeRequestAsync] Lỗi khi gửi notification: {ex.Message}");
        }

        return await GetTradeRequestByIdAsync(tradeRequest.Id);
    }

    public async Task<TradeRequestDto> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted)
    {

        try
        {
            // BƯỚC 1: Lấy trade request với đầy đủ thông tin liên quan
            var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
                t => t.Listing!,
                t => t.Listing!.InventoryItem,
                t => t.Listing!.InventoryItem.Product!,
                t => t.Requester!);

            if (tradeRequest == null)
            {
                throw ErrorHelper.NotFound("Trade Request không tồn tại.");
            }


            // BƯỚC 2: Validate trạng thái
            if (tradeRequest.Status != TradeRequestStatus.PENDING)
            {
                throw ErrorHelper.BadRequest("Giao dịch này đã được xử lý hoặc hết hạn.");
            }

            // BƯỚC 3: Validate listing
            if (tradeRequest.Listing == null)
            {
                throw ErrorHelper.Internal("Thông tin listing không hợp lệ.");
            }

            // BƯỚC 4: Validate inventory item
            if (tradeRequest.Listing.InventoryItem == null)
            {
                throw ErrorHelper.Internal("Thông tin inventory item không hợp lệ.");
            }

            // BƯỚC 5: Kiểm tra quyền respond (chỉ owner của listing mới được respond)
            var currentUserId = _claimsService.CurrentUserId;
            if (tradeRequest.Listing.InventoryItem.UserId != currentUserId)
            {
                throw ErrorHelper.Forbidden("Bạn không có quyền phản hồi trade request này.");
            }

            // BƯỚC 6: Cập nhật trạng thái trade request
            var originalStatus = tradeRequest.Status;
            tradeRequest.Status = isAccepted ? TradeRequestStatus.ACCEPTED : TradeRequestStatus.REJECTED;
            tradeRequest.RespondedAt = DateTime.UtcNow;

            // Reset TimeRemaining dựa trên trạng thái mới
            if (isAccepted)
                // Nếu accept, tính toán TimeRemaining = 2 phút = 120 giây
                tradeRequest.TimeRemaining = 120;
            else
                // Nếu reject, TimeRemaining = 0
                tradeRequest.TimeRemaining = 0;

            // BƯỚC 7: Xử lý inventory item status
            await UpdateInventoryItemStatusOnReject(tradeRequest, isAccepted);

            // BƯỚC 8: Lưu thay đổi
            await _unitOfWork.TradeRequests.Update(tradeRequest);
            await _unitOfWork.SaveChangesAsync();

            // BƯỚC 9: Gửi notification (với error handling)
            try
            {
                await SendTradeResponseNotificationSafe(tradeRequest, currentUserId, isAccepted);
            }
            catch (Exception notificationEx)
            {
                _logger.Error($"[RespondTradeRequestAsync] Lỗi khi gửi notification: {notificationEx.Message}");
                // Không throw để không ảnh hưởng đến logic chính
            }

            // BƯỚC 10: Trả về kết quả
            return await GetTradeRequestByIdAsync(tradeRequestId);
        }
        catch (Exception ex)
        {
            _logger.Error($"[RespondTradeRequestAsync] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId)
    {
        _logger.Info($"[LockDealAsync] Bắt đầu xử lý lock deal cho trade request {tradeRequestId}");

        try
        {
            // BƯỚC 1: LẤY THÔNG TIN USER HIỆN TẠI
            var userId = _claimsService.CurrentUserId;
            _logger.Info($"[LockDealAsync] Current user: {userId}");

            // BƯỚC 2: TẢI TRADE REQUEST VÀ CÁC THÔNG TIN LIÊN QUAN
            var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
                t => t.Listing!, // Thông tin listing được trade
                t => t.Listing!.InventoryItem, // Item trong listing
                t => t.Listing!.InventoryItem.Product!, // Chi tiết sản phẩm
                t => t.OfferedItems, // Các item được đề xuất trao đổi
                t => t.Requester!); // Người gửi yêu cầu trade

            // BƯỚC 3: VALIDATE TRADE REQUEST TỒN TẠI
            if (tradeRequest == null)
            {
                _logger.Error($"[LockDealAsync] Trade request {tradeRequestId} không tồn tại");
                throw ErrorHelper.NotFound("Trade request không tồn tại.");
            }

            _logger.Info($"[LockDealAsync] Tìm thấy trade request {tradeRequestId}, status: {tradeRequest.Status}");

            // BƯỚC 4: VALIDATE TRẠNG THÁI TRADE REQUEST
            if (tradeRequest.Status != TradeRequestStatus.ACCEPTED)
            {
                _logger.Warn(
                    $"[LockDealAsync] Trade request {tradeRequestId} không ở trạng thái ACCEPTED, status hiện tại: {tradeRequest.Status}");
                throw ErrorHelper.BadRequest("Trade request chưa được chấp nhận.");
            }

            // BƯỚC 5: VALIDATE LISTING VÀ INVENTORY ITEM
            if (tradeRequest.Listing == null)
            {
                _logger.Error($"[LockDealAsync] Listing null cho trade request {tradeRequestId}");
                throw ErrorHelper.Internal("Thông tin listing không hợp lệ.");
            }

            if (tradeRequest.Listing.InventoryItem == null)
            {
                _logger.Error($"[LockDealAsync] InventoryItem null cho listing {tradeRequest.Listing.Id}");
                throw ErrorHelper.Internal("Thông tin inventory item không hợp lệ.");
            }

            // BƯỚC 6: XÁC ĐỊNH LISTING OWNER (User A)
            var listingOwnerId = tradeRequest.Listing.InventoryItem.UserId;
            _logger.Info($"[LockDealAsync] Listing owner: {listingOwnerId}, Requester: {tradeRequest.RequesterId}");

            // BƯỚC 7: VALIDATE USER CÓ QUYỀN LOCK
            var isOwner = userId == listingOwnerId;
            var isRequester = userId == tradeRequest.RequesterId;

            if (!isOwner && !isRequester)
            {
                _logger.Warn(
                    $"[LockDealAsync] User {userId} không phải owner ({listingOwnerId}) hoặc requester ({tradeRequest.RequesterId})");
                throw ErrorHelper.Forbidden("Bạn không có quyền lock giao dịch này.");
            }

            _logger.Info(
                $"[LockDealAsync] User {userId} có quyền lock - IsOwner: {isOwner}, IsRequester: {isRequester}");

            // BƯỚC 8: KIỂM TRA TRẠNG THÁI LOCK HIỆN TẠI
            _logger.Info(
                $"[LockDealAsync] Trạng thái lock hiện tại - OwnerLocked: {tradeRequest.OwnerLocked}, RequesterLocked: {tradeRequest.RequesterLocked}");

            // Kiểm tra user đã lock trước đó chưa
            if (isOwner && tradeRequest.OwnerLocked)
            {
                _logger.Warn($"[LockDealAsync] Owner {userId} đã lock trước đó");
                throw ErrorHelper.BadRequest("Bạn đã lock giao dịch này rồi.");
            }

            if (isRequester && tradeRequest.RequesterLocked)
            {
                _logger.Warn($"[LockDealAsync] Requester {userId} đã lock trước đó");
                throw ErrorHelper.BadRequest("Bạn đã lock giao dịch này rồi.");
            }

            // BƯỚC 9: XỬ LÝ LOGIC LOCK VÀ KIỂM TRA HOÀN THÀNH
            await ProcessLockAndCompleteIfReadySafe(tradeRequest, userId, listingOwnerId, isOwner, isRequester);

            // BƯỚC 10: GỬI REAL-TIME NOTIFICATION QUA SIGNALR
            try
            {
                await SendRealTimeLockNotificationSafe(tradeRequest, userId, listingOwnerId);
            }
            catch (Exception signalREx)
            {
                _logger.Error($"[LockDealAsync] Lỗi khi gửi SignalR notification: {signalREx.Message}");
                // Không throw để không ảnh hưởng logic chính
            }

            // BƯỚC 11: LƯU THAY ĐỔI VÀO DATABASE
            await _unitOfWork.TradeRequests.Update(tradeRequest);
            await _unitOfWork.SaveChangesAsync();

            _logger.Success($"[LockDealAsync] Đã lưu thành công trade request {tradeRequestId}");

            // BƯỚC 12: GỬI NOTIFICATION THÔNG THƯỜNG (BACKUP)
            try
            {
                await SendDealLockedNotificationSafe(tradeRequest, listingOwnerId);
            }
            catch (Exception notificationEx)
            {
                _logger.Error($"[LockDealAsync] Lỗi khi gửi notification: {notificationEx.Message}");
                // Không throw để không ảnh hưởng logic chính
            }

            // BƯỚC 13: TRẢ VỀ KẾT QUẢ
            _logger.Info($"[LockDealAsync] Hoàn thành xử lý lock deal cho trade request {tradeRequestId}");
            return await GetTradeRequestByIdAsync(tradeRequestId);
        }
        catch (Exception ex)
        {
            _logger.Error($"[LockDealAsync] Lỗi khi xử lý lock deal cho trade request {tradeRequestId}: {ex.Message}");
            _logger.Error($"[LockDealAsync] Stack trace: {ex.StackTrace}");
            throw;
        }
    }


    public async Task ReleaseHeldItemsAsync()
    {
        var now = DateTime.UtcNow;
        var itemsToRelease = await _unitOfWork.InventoryItems.GetAllAsync(
            i => i.Status == InventoryItemStatus.OnHold &&
                 i.HoldUntil.HasValue &&
                 i.HoldUntil.Value <= now,
            i => i.Product!); // Include Product để có thể lấy tên

        if (!itemsToRelease.Any()) return;

        foreach (var item in itemsToRelease)
        {
            item.Status = InventoryItemStatus.Available;
            item.HoldUntil = null;
        }

        await _unitOfWork.InventoryItems.UpdateRange(itemsToRelease);
        await _unitOfWork.SaveChangesAsync();

        // Gửi notification cho từng item được release
        foreach (var item in itemsToRelease)
            try
            {
                await NotifyItemReleased(item);
            }
            catch (Exception ex)
            {
                // Không throw exception để không ảnh hưởng việc release các item khác
            }
    }

    public async Task<TradeRequestDto> GetTradeRequestByIdAsync(Guid tradeRequestId)
    {
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
            t => t.Listing!,
            t => t.Listing!.InventoryItem,
            t => t.Listing!.InventoryItem.Product!,
            t => t.Requester!,
            t => t.OfferedItems);

        if (tradeRequest == null) throw ErrorHelper.NotFound("Trade Request không tồn tại.");

        var offeredInventoryItems = new List<InventoryItem>();
        if (tradeRequest.OfferedItems.Any())
        {
            var itemIds = tradeRequest.OfferedItems.Select(oi => oi.InventoryItemId).ToList();

            offeredInventoryItems = await _unitOfWork.InventoryItems.GetQueryable()
                .Include(i => i.Product)
                .Where(i => itemIds.Contains(i.Id))
                .ToListAsync();
        }

        var dto = MapTradeRequestToDto(tradeRequest, offeredInventoryItems);
        return dto;
    }

    #region Private Methods

    private async Task CreateTradeHistoryAsync(TradeRequest tradeRequest, TradeRequestStatus finalStatus)
    {
        var tradeHistory = new TradeHistory
        {
            ListingId = tradeRequest.ListingId,
            RequesterId = tradeRequest.RequesterId,
            OfferedInventoryId =
                tradeRequest.OfferedItems.FirstOrDefault()?.InventoryItemId, // Lấy item đầu tiên nếu có nhiều
            FinalStatus = finalStatus,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = tradeRequest.CreatedAt
        };

        await _unitOfWork.TradeHistories.AddAsync(tradeHistory);
    }

    // Method helper mới để xử lý lock logic an toàn hơn
    private async Task CompleteTradeExchangeAsync(TradeRequest tradeRequest)
    {
        _logger.Info($"[CompleteTradeExchangeAsync] Bắt đầu trao đổi item cho trade request {tradeRequest.Id}");

        try
        {
            // 1. Lấy thông tin listing item (item của owner)
            var listingItem = tradeRequest.Listing!.InventoryItem;
            var originalOwnerId = listingItem.UserId;
            var newOwnerId = tradeRequest.RequesterId;

            _logger.Info(
                $"[CompleteTradeExchangeAsync] Listing item {listingItem.Id} sẽ chuyển từ {originalOwnerId} sang {newOwnerId}");

            // 2. Lấy thông tin offered items (items của requester)
            var offeredItemIds = tradeRequest.OfferedItems.Select(oi => oi.InventoryItemId).ToList();
            var offeredItems = await _unitOfWork.InventoryItems.GetAllAsync(
                i => offeredItemIds.Contains(i.Id),
                i => i.Product!);

            _logger.Info($"[CompleteTradeExchangeAsync] Sẽ trao đổi {offeredItems.Count} offered items");

            // 3. Trao đổi ownership của listing item
            listingItem.UserId = newOwnerId; // Chuyển item của owner sang requester
            listingItem.Status = InventoryItemStatus.OnHold; // Đặt OnHold 3 ngày để tránh trade liên tục
            listingItem.HoldUntil = DateTime.UtcNow.AddDays(3);
            listingItem.LockedByRequestId = null; // Reset lock

            await _unitOfWork.InventoryItems.Update(listingItem);

            // 4. Trao đổi ownership của offered items
            foreach (var offeredItem in offeredItems)
            {
                _logger.Info(
                    $"[CompleteTradeExchangeAsync] Chuyển offered item {offeredItem.Id} từ {offeredItem.UserId} sang {originalOwnerId}");

                offeredItem.UserId = originalOwnerId; // Chuyển item của requester sang owner
                offeredItem.Status = InventoryItemStatus.OnHold; // Đặt OnHold 3 ngày
                offeredItem.HoldUntil = DateTime.UtcNow.AddDays(3);

                await _unitOfWork.InventoryItems.Update(offeredItem);
            }

            // 5. Cập nhật trạng thái trade request
            tradeRequest.Status = TradeRequestStatus.COMPLETED;
            tradeRequest.RespondedAt = DateTime.UtcNow;

            // 6. Cập nhật trạng thái listing thành inactive
            var listing = tradeRequest.Listing;
            listing.Status = ListingStatus.Sold;
            await _unitOfWork.Listings.Update(listing);

            // 7. Tạo trade history record
            await CreateTradeHistoryAsync(tradeRequest, TradeRequestStatus.COMPLETED);

            _logger.Success($"[CompleteTradeExchangeAsync] Hoàn thành trao đổi cho trade request {tradeRequest.Id}");
        }
        catch (Exception ex)
        {
            _logger.Error($"[CompleteTradeExchangeAsync] Lỗi khi trao đổi item: {ex.Message}");
            throw;
        }
    }

    // Method helper để gửi SignalR notification an toàn
    private async Task SendRealTimeLockNotificationSafe(TradeRequest tradeRequest, Guid currentUserId,
        Guid listingOwnerId)
    {
        try
        {
            var otherUserId = currentUserId == listingOwnerId ? tradeRequest.RequesterId : listingOwnerId;

            _logger.Info($"[SendRealTimeLockNotificationSafe] Gửi SignalR notification cho user {otherUserId}");

            await _notificationHub.Clients.User(otherUserId.ToString())
                .SendAsync("TradeRequestLocked", new
                {
                    TradeRequestId = tradeRequest.Id,
                    Message = "Đối tác đã lock giao dịch. Hãy kiểm tra!",
                    OwnerLocked = tradeRequest.OwnerLocked,
                    RequesterLocked = tradeRequest.RequesterLocked,
                    TimeRemaining = tradeRequest.TimeRemaining
                });

            _logger.Success(
                $"[SendRealTimeLockNotificationSafe] Đã gửi SignalR notification thành công cho user {otherUserId}");
        }
        catch (Exception ex)
        {
            _logger.Error($"[SendRealTimeLockNotificationSafe] Lỗi khi gửi SignalR notification: {ex.Message}");
            throw;
        }
    }

    // Method helper để gửi notification thông thường an toàn
    private async Task SendDealLockedNotificationSafe(TradeRequest tradeRequest, Guid listingOwnerId)
    {
        try
        {
            var requester = await _unitOfWork.Users.GetByIdAsync(tradeRequest.RequesterId);
            var listingOwner = await _unitOfWork.Users.GetByIdAsync(listingOwnerId);

            if (requester == null)
            {
                _logger.Warn($"[SendDealLockedNotificationSafe] Không tìm thấy requester {tradeRequest.RequesterId}");
                return;
            }

            if (listingOwner == null)
            {
                _logger.Warn($"[SendDealLockedNotificationSafe] Không tìm thấy listing owner {listingOwnerId}");
                return;
            }

            await SendDealLockedNotificationAsync(requester, listingOwner);
            _logger.Success(
                $"[SendDealLockedNotificationSafe] Đã gửi notification thành công cho trade request {tradeRequest.Id}");
        }
        catch (Exception ex)
        {
            _logger.Error($"[SendDealLockedNotificationSafe] Lỗi khi gửi notification: {ex.Message}");
            throw;
        }
    }

    private async Task NotifyItemReleased(InventoryItem item)
    {
        await _notificationService.PushNotificationToUser(
            item.UserId,
            new NotificationDto
            {
                Title = "Vật phẩm sẵn sàng trade!",
                Message = $"'{item.Product?.Name}' đã có thể trao đổi lại.",
                Type = NotificationType.System
            }
        );
    }

    private async Task SendTradeResponseNotificationSafe(TradeRequest tradeRequest, Guid responderId, bool isAccepted)
    {
        try
        {
            if (tradeRequest.Requester == null)
            {
                _logger.Warn(
                    $"[SendTradeResponseNotificationSafe] Requester null cho trade request {tradeRequest.Id}, cố gắng load từ database");

                var requester = await _unitOfWork.Users.GetByIdAsync(tradeRequest.RequesterId);
                if (requester == null)
                {
                    _logger.Error(
                        $"[SendTradeResponseNotificationSafe] Không thể tìm thấy requester {tradeRequest.RequesterId}");
                    return;
                }

                tradeRequest.Requester = requester;
            }

            var responder = await _unitOfWork.Users.GetByIdAsync(responderId);
            if (responder == null)
            {
                _logger.Error($"[SendTradeResponseNotificationSafe] Không thể tìm thấy responder {responderId}");
                return;
            }

            // Truyền thêm tradeRequestId vào method
            await SendTradeResponseNotificationAsync(tradeRequest.Requester, responder.FullName ?? "Unknown",
                isAccepted, tradeRequest.Id);

            _logger.Success(
                $"[SendTradeResponseNotificationSafe] Đã gửi notification thành công cho trade request {tradeRequest.Id}");
        }
        catch (Exception ex)
        {
            _logger.Error($"[SendTradeResponseNotificationSafe] Lỗi khi gửi notification: {ex.Message}");
        }
    }

    private async Task SendTradeRequestNotificationIfNotSentAsync(User user, string? requesterName)
    {
        var message = string.IsNullOrEmpty(requesterName)
            ? "Bạn có một yêu cầu trao đổi vật phẩm mới. Hãy kiểm tra và phản hồi sớm nhé!"
            : $"{requesterName} đã gửi yêu cầu trao đổi vật phẩm với bạn. Hãy kiểm tra ngay!";

        await _notificationService.PushNotificationToUser(
            user.Id,
            new NotificationDto
            {
                Title = "Yêu cầu trao đổi mới!",
                Message = message,
                Type = NotificationType.Trading
            }
        );
    }

    private async Task SendTradeResponseNotificationAsync(User requester, string responderName, bool isAccepted,
        Guid tradeRequestId)
    {
        var title = isAccepted ? "Yêu cầu trao đổi được chấp nhận!" : "Yêu cầu trao đổi bị từ chối";
        var message = isAccepted
            ? $"{responderName} đã chấp nhận yêu cầu trao đổi của bạn. Hãy xác nhận để hoàn tất giao dịch!"
            : $"{responderName} đã từ chối yêu cầu trao đổi của bạn.";

        var sourceUrl = isAccepted ? $"/marketplace/confirm-trading/{tradeRequestId}" : null;

        await _notificationService.PushNotificationToUser(
            requester.Id,
            new NotificationDto
            {
                Title = title,
                Message = message,
                Type = NotificationType.Trading,
                SourceUrl = sourceUrl // Null cho reject, có URL cho accept
            }
        );
    }

    private async Task SendDealLockedNotificationAsync(User requester, User listingOwner)
    {
        var notifications = new[]
        {
            new
            {
                User = requester, Message = "Giao dịch đã được khóa! Hãy liên hệ với đối tác để hoàn tất việc trao đổi."
            },
            new
            {
                User = listingOwner,
                Message = "Giao dịch đã được khóa! Hãy liên hệ với đối tác để hoàn tất việc trao đổi."
            }
        };

        foreach (var noti in notifications)
            await _notificationService.PushNotificationToUser(
                noti.User.Id,
                new NotificationDto
                {
                    Title = "Giao dịch đã được khóa!",
                    Message = noti.Message,
                    Type = NotificationType.Trading
                }
            );
    }

    private async Task ValidateMultipleOfferedItems(List<Guid> offeredInventoryIds,
        Listing listing, Guid userId)
    {
        _logger.Info(
            $"[ValidateMultipleOfferedItems] Bắt đầu validate {offeredInventoryIds.Count} offered items cho user {userId}");

        // Kiểm tra duplicate items
        var duplicateIds = offeredInventoryIds.GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
        {
            _logger.Warn(
                $"[ValidateMultipleOfferedItems] Phát hiện duplicate items: {string.Join(", ", duplicateIds)}");
            throw ErrorHelper.BadRequest("Không thể đề xuất cùng một item nhiều lần trong một giao dịch.");
        }

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

        // Tiếp tục với logic kiểm tra OnHold như cũ...
        var now = DateTime.UtcNow;
        var heldItems = offeredItems.Where(i =>
            i.Status == InventoryItemStatus.OnHold &&
            i.HoldUntil.HasValue &&
            i.HoldUntil.Value > now).ToList();

        if (heldItems.Any())
        {
            var heldItem = heldItems.First();
            var remainingTime = heldItem.HoldUntil!.Value - now;
            _logger.Warn(
                $"[ValidateMultipleOfferedItems] Item {heldItem.Id} đang trong thời gian giữ, còn {remainingTime.TotalDays:F1} ngày");
            throw ErrorHelper.BadRequest(
                $"Vật phẩm '{heldItem.Product?.Name}' đang trong thời gian chờ xử lý sau giao dịch. Vui lòng thử lại sau {remainingTime.TotalDays:F1} ngày.");
        }

        _logger.Success($"[ValidateMultipleOfferedItems] Validate thành công {offeredItems.Count} items");
    }

    private async Task<Listing> ValidateTradeRequestCreation(Guid listingId, Guid userId)
    {
        _logger.Info(
            $"[ValidateTradeRequestCreation] Bắt đầu kiểm tra điều kiện tạo trade request cho listing {listingId}");

        // Sử dụng ListingService để kiểm tra tồn tại của listing
        try
        {
            await _listingService.GetListingByIdAsync(listingId);
            _logger.Info($"[ValidateTradeRequestCreation] Đã tìm thấy listing {listingId} qua ListingService");

            // Sau khi xác nhận listing tồn tại, lấy thông tin đầy đủ từ database
            var listing = await _unitOfWork.Listings.GetByIdAsync(listingId,
                l => l.InventoryItem,
                l => l.InventoryItem.User!);

            if (listing == null)
            {
                _logger.Error(
                    $"[ValidateTradeRequestCreation] Tình huống bất thường: ListingService tìm thấy ID {listingId} nhưng Repository không tìm thấy");
                throw ErrorHelper.Internal("Có lỗi khi xác thực thông tin listing.");
            }

            if (listing.Status != ListingStatus.Active)
            {
                _logger.Warn(
                    $"[ValidateTradeRequestCreation] Listing {listingId} không còn hoạt động, status: {listing.Status}");
                throw ErrorHelper.BadRequest("Listing không còn hoạt động.");
            }

            // Kiểm tra người dùng không tạo trade cho listing của chính mình
            if (listing.InventoryItem.UserId == userId)
            {
                _logger.Warn(
                    $"[ValidateTradeRequestCreation] User {userId} cố gắng tạo trade request cho listing của chính mình {listing.Id}");
                throw ErrorHelper.BadRequest("Bạn không thể tạo yêu cầu trao đổi với chính mình.");
            }

            // Kiểm tra trạng thái giữ của listing item
            var now = DateTime.UtcNow;
            var listingItem = listing.InventoryItem;
            if (listingItem.Status == InventoryItemStatus.OnHold && listingItem.HoldUntil.HasValue &&
                listingItem.HoldUntil.Value > now)
            {
                var remainingTime = listingItem.HoldUntil.Value - now;
                _logger.Warn(
                    $"[ValidateTradeRequestCreation] Listing item {listingItem.Id} đang trong thời gian giữ, còn {remainingTime.TotalDays:F1} ngày");
                throw ErrorHelper.BadRequest(
                    $"Vật phẩm này đang trong thời gian chờ xử lý sau giao dịch. Vui lòng thử lại sau {remainingTime.TotalDays:F1} ngày.");
            }

            // Kiểm tra người dùng đã tạo trade request cho listing này chưa
            var existingTradeRequest = await _unitOfWork.TradeRequests.FirstOrDefaultAsync(tr =>
                tr.ListingId == listingId &&
                tr.RequesterId == userId &&
                tr.Status == TradeRequestStatus.PENDING);

            if (existingTradeRequest != null)
            {
                _logger.Warn(
                    $"[ValidateTradeRequestCreation] User {userId} đã có trade request pending cho listing {listing.Id}");
                throw ErrorHelper.BadRequest("Bạn đã tạo yêu cầu trao đổi cho listing này rồi. Vui lòng chờ phản hồi.");
            }

            _logger.Success(
                $"[ValidateTradeRequestCreation] Kiểm tra điều kiện tạo trade request cho listing {listingId} thành công");
            return listing;
        }
        catch (Exception ex) when (ex.Message.Contains("Listing không tồn tại"))
        {
            _logger.Warn(
                $"[ValidateTradeRequestCreation] ListingService không tìm thấy listing {listingId}: {ex.Message}");
            throw ErrorHelper.NotFound("Listing không tồn tại.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[ValidateTradeRequestCreation] Lỗi khi kiểm tra listing {listingId}: {ex.Message}");
            throw;
        }
    }

    private async Task<List<InventoryItem>> ValidateItemsOwnership(List<Guid> itemIds, Guid userId)
    {
        var now = DateTime.UtcNow;

        var items = await _unitOfWork.InventoryItems.GetAllAsync(
            i => itemIds.Contains(i.Id),
            i => i.Product!);

        if (items.Count != itemIds.Count)
            throw ErrorHelper.BadRequest("Một số item bạn muốn đổi không hợp lệ.");

        foreach (var item in items)
        {
            if (item.UserId != userId)
                throw ErrorHelper.BadRequest($"Item '{item.Product?.Name}' không thuộc về bạn.");

            if (item.Status == InventoryItemStatus.OnHold && item.HoldUntil.HasValue && item.HoldUntil.Value <= now)
            {
                // Tự động cập nhật trạng thái item nếu đã hết thời gian giữ
                item.Status = InventoryItemStatus.Available;
                item.HoldUntil = null;
                await _unitOfWork.InventoryItems.Update(item);
            }
            else if (item.Status != InventoryItemStatus.Available)
            {
                throw ErrorHelper.BadRequest($"Item '{item.Product?.Name}' không khả dụng.");
            }
        }

        return items;
    }

    private async Task UpdateInventoryItemStatusOnReject(TradeRequest tradeRequest, bool isAccepted)
    {
        _logger.Info(
            $"[UpdateInventoryItemStatusOnReject] Bắt đầu xử lý reject status - TradeRequestId: {tradeRequest.Id}, IsAccepted: {isAccepted}");

        try
        {
            if (!isAccepted)
            {
                _logger.Info(
                    $"[UpdateInventoryItemStatusOnReject] Trade request {tradeRequest.Id} bị reject, khôi phục status listing item");

                // Kiểm tra null safety
                if (tradeRequest.Listing == null)
                {
                    _logger.Error(
                        $"[UpdateInventoryItemStatusOnReject] Listing null cho trade request {tradeRequest.Id}");
                    return;
                }

                var listingItem = tradeRequest.Listing.InventoryItem;

                _logger.Info(
                    $"[UpdateInventoryItemStatusOnReject] Khôi phục listing item {listingItem.Id} từ status {listingItem.Status} về Available");

                listingItem.Status = InventoryItemStatus.Available;
                listingItem.LockedByRequestId = null; // Reset lock nếu có

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
        catch (Exception ex)
        {
            _logger.Error($"[UpdateInventoryItemStatusOnReject] Lỗi khi cập nhật inventory status: {ex.Message}");
            throw;
        }
    }

    private async Task ProcessLockAndCompleteIfReadySafe(TradeRequest tradeRequest, Guid userId, Guid listingOwnerId,
        bool isOwner, bool isRequester)
    {
        _logger.Info($"[ProcessLockAndCompleteIfReadySafe] Xử lý lock logic cho trade request {tradeRequest.Id}");

        try
        {
            // Cập nhật trạng thái lock
            if (isOwner)
            {
                _logger.Info($"[ProcessLockAndCompleteIfReadySafe] Owner {userId} thực hiện lock");
                tradeRequest.OwnerLocked = true;
            }
            else if (isRequester)
            {
                _logger.Info($"[ProcessLockAndCompleteIfReadySafe] Requester {userId} thực hiện lock");
                tradeRequest.RequesterLocked = true;
            }

            _logger.Info(
                $"[ProcessLockAndCompleteIfReadySafe] Trạng thái lock sau cập nhật - OwnerLocked: {tradeRequest.OwnerLocked}, RequesterLocked: {tradeRequest.RequesterLocked}");

            // Kiểm tra nếu cả hai đã lock
            if (tradeRequest.OwnerLocked && tradeRequest.RequesterLocked)
            {
                _logger.Info($"[ProcessLockAndCompleteIfReadySafe] Cả hai bên đã lock, bắt đầu hoàn thành giao dịch");

                tradeRequest.LockedAt = DateTime.UtcNow;
                tradeRequest.TimeRemaining = 0;

                // THỰC HIỆN TRAO ĐỔI ITEM
                await CompleteTradeExchangeAsync(tradeRequest);

                _logger.Success(
                    $"[ProcessLockAndCompleteIfReadySafe] Hoàn thành trao đổi cho trade request {tradeRequest.Id}");
            }
            else
            {
                _logger.Info($"[ProcessLockAndCompleteIfReadySafe] Chưa đủ điều kiện hoàn thành, chờ bên còn lại lock");

                // Tính toán thời gian còn lại
                if (tradeRequest.RespondedAt.HasValue)
                {
                    var timeoutMinutes = 2;
                    var elapsedTime = DateTime.UtcNow - tradeRequest.RespondedAt.Value;
                    var remainingTime = TimeSpan.FromMinutes(timeoutMinutes) - elapsedTime;
                    tradeRequest.TimeRemaining = remainingTime.TotalSeconds > 0 ? (int)remainingTime.TotalSeconds : 0;

                    _logger.Info(
                        $"[ProcessLockAndCompleteIfReadySafe] Cập nhật TimeRemaining = {tradeRequest.TimeRemaining} giây");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[ProcessLockAndCompleteIfReadySafe] Lỗi trong quá trình xử lý lock: {ex.Message}");
            throw;
        }
    }

    private static TradeRequestDto MapTradeRequestToDto(TradeRequest tradeRequest, List<InventoryItem> offeredItems)
    {
        var listingItem = tradeRequest.Listing?.InventoryItem;
        var listingItemName = listingItem?.Product?.Name ?? "Unknown";
        var listingItemImgUrl = listingItem?.Product?.ImageUrls?.FirstOrDefault();
        var listingItemTier = listingItem?.Tier ?? RarityName.Common;
        var requesterName = tradeRequest.Requester?.FullName ?? "Unknown";
        var listingOwnerName = tradeRequest.Listing?.InventoryItem.User?.FullName ?? "Unknown";
        var listingOwnerAvatarUrl = tradeRequest.Listing?.InventoryItem.User?.AvatarUrl;
        var requesterAvatarUrl = tradeRequest.Requester?.AvatarUrl;

        var offeredItemDtos = offeredItems.Select(item => new OfferedItemDto
        {
            InventoryItemId = item.Id,
            ItemName = item.Product?.Name,
            ImageUrl = item.Product?.ImageUrls?.FirstOrDefault(),
            Tier = item.Tier ?? RarityName.Common
        }).ToList();

        // Tính toán thời gian còn lại
        var timeRemaining = 0;
        if (tradeRequest.Status == TradeRequestStatus.ACCEPTED && tradeRequest.RespondedAt.HasValue)
        {
            var timeoutMinutes = 2; // 2 phút timeout
            var elapsedTime = DateTime.UtcNow - tradeRequest.RespondedAt.Value;
            var remainingTime = TimeSpan.FromMinutes(timeoutMinutes) - elapsedTime;

            // Nếu còn thời gian thì tính bằng giây, nếu không thì = 0
            timeRemaining = remainingTime.TotalSeconds > 0 ? (int)remainingTime.TotalSeconds : 0;
        }

        var dto = new TradeRequestDto
        {
            Id = tradeRequest.Id,
            ListingId = tradeRequest.ListingId,
            ListingItemName = listingItemName,
            ListingItemTier = listingItemTier,
            ListingItemImgUrl = listingItemImgUrl,
            RequesterId = tradeRequest.RequesterId,
            RequesterName = requesterName,
            ListingOwnerName = listingOwnerName,
            ListingOwnerAvatarUrl = listingOwnerAvatarUrl,
            RequesterAvatarUrl = requesterAvatarUrl,
            OfferedItems = offeredItemDtos,
            Status = tradeRequest.Status,
            RequestedAt = tradeRequest.RequestedAt,
            RespondedAt = tradeRequest.RespondedAt,
            OwnerLocked = tradeRequest.OwnerLocked,
            RequesterLocked = tradeRequest.RequesterLocked,
            LockedAt = tradeRequest.LockedAt,
            TimeRemaining = timeRemaining // Gán giá trị tính toán được
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