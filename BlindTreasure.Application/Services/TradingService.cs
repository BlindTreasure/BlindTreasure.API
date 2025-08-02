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
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IListingService _listingService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<NotificationHub> _notificationHub;


    public TradingService(IClaimsService claimsService, ILoggerService logger,
        IUnitOfWork unitOfWork, INotificationService notificationService, ICacheService cacheService,
        IListingService listingService)
    {
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _cacheService = cacheService;
        _listingService = listingService;
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

    public async Task<Pagination<TradeHistoryDto>> GetMyTradeHistoriesAsync(TradeHistoryQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;

        _logger.Info(
            $"[GetMyTradeHistoriesAsync] Lấy trade histories của user {userId}, Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.TradeHistories.GetQueryable()
            .Include(th => th.Listing)
            .ThenInclude(l => l.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(th => th.Requester)
            .Include(th => th.OfferedInventory)
            .ThenInclude(oi => oi!.Product)
            .Where(th => !th.IsDeleted && th.RequesterId == userId) // Chỉ lấy của user hiện tại
            .AsNoTracking();

        // Apply filters (trừ RequesterId vì đã filter ở trên)
        var filteredParam = new TradeHistoryQueryParameter
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

        query = ApplyTradeHistoryFilters(query, filteredParam);
        query = ApplyTradeHistorySorting(query, filteredParam);

        var count = await query.CountAsync();

        var tradeHistories = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var tradeHistoryDtos = tradeHistories.Select(MapTradeHistoryToDto).ToList();

        _logger.Success($"[GetMyTradeHistoriesAsync] Tìm thấy {count} trade histories của user {userId}");

        return new Pagination<TradeHistoryDto>(tradeHistoryDtos, count, param.PageIndex, param.PageSize);
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

    public async Task<List<TradeRequestDto>> GetMyTradeRequestsAsync()
    {
        var userId = _claimsService.CurrentUserId;

        var tradeRequests = await _unitOfWork.TradeRequests.GetAllAsync(
            t => t.RequesterId == userId,
            t => t.Listing!,
            t => t.Listing!.InventoryItem,
            t => t.Listing!.InventoryItem.Product!,
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
            dtos.Add(dto);
        }

        return dtos.OrderByDescending(x => x.RequestedAt).ToList();
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

        try
        {
            var requester = await _unitOfWork.Users.GetByIdAsync(tradeRequest.RequesterId);
            var responder = await _unitOfWork.Users.GetByIdAsync(_claimsService.CurrentUserId);

            if (requester != null && responder != null)
                await SendTradeResponseNotificationAsync(requester, responder.FullName, isAccepted);
        }
        catch (Exception ex)
        {
            _logger.Error($"[RespondTradeRequestAsync] Lỗi khi gửi notification: {ex.Message}");
        }

        return await GetTradeRequestByIdAsync(tradeRequestId);
    }

    public async Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId)
    {
        // BƯỚC 1: LẤY THÔNG TIN USER HIỆN TẠI
        var userId = _claimsService.CurrentUserId;

        // BƯỚC 2: TẢI TRADE REQUEST VÀ CÁC THÔNG TIN LIÊN QUAN
        var tradeRequest = await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId,
            t => t.Listing!, // Thông tin listing được trade
            t => t.Listing!.InventoryItem, // Item trong listing
            t => t.Listing!.InventoryItem.Product!, // Chi tiết sản phẩm
            t => t.OfferedItems, // Các item được đề xuất trao đổi
            t => t.Requester!); // Người gửi yêu cầu trade

        // BƯỚC 3: VALIDATE TRADE REQUEST TỒN TẠI
        if (tradeRequest == null)
            throw ErrorHelper.NotFound("Trade request không tồn tại.");

        // BƯỚC 4: VALIDATE TRẠNG THÁI TRADE REQUEST
        if (tradeRequest.Status != TradeRequestStatus.ACCEPTED)
            throw ErrorHelper.BadRequest("Trade request chưa được chấp nhận.");

        // BƯỚC 5: XÁC ĐỊNH LISTING OWNER (User A)
        // User A = người sở hữu item trong listing
        var listingOwnerId = tradeRequest.Listing!.InventoryItem.UserId;

        // BƯỚC 6: XỬ LÝ LOGIC LOCK VÀ KIỂM TRA HOÀN THÀNH
        // - Nếu cả 2 đã lock → tự động complete trade
        await ProcessLockAndCompleteIfReady(tradeRequest, userId, listingOwnerId);

        // BƯỚC 7: GỬI REAL-TIME NOTIFICATION QUA SIGNALR
        // - Thông báo cho user còn lại rằng đối tác đã lock
        // - Update UI real-time để hiển thị trạng thái lock
        await SendRealTimeLockNotification(tradeRequest, userId, listingOwnerId);

        // BƯỚC 8: LƯU THAY ĐỔI VÀO DATABASE
        await _unitOfWork.TradeRequests.Update(tradeRequest);
        await _unitOfWork.SaveChangesAsync();

        // BƯỚC 9: LẤY LẠI TRADE REQUEST ĐÃ CẬP NHẬT
        await _unitOfWork.TradeRequests.GetByIdAsync(tradeRequestId);

        // BƯỚC 10: GỬI NOTIFICATION THÔNG THƯỜNG (BACKUP)
        try
        {
            var requester = await _unitOfWork.Users.GetByIdAsync(tradeRequest.RequesterId); // User B
            var listingOwner = await _unitOfWork.Users.GetByIdAsync(listingOwnerId); // User A

            if (requester != null && listingOwner != null)
                await SendDealLockedNotificationAsync(requester, listingOwner);
        }
        catch (Exception ex)
        {
            _logger.Error($"[LockDealAsync] Lỗi khi gửi notification: {ex.Message}");
        }

        // BƯỚC 11: TRẢ VỀ KẾT QUẢ
        return await GetTradeRequestByIdAsync(tradeRequestId);
    }

    public async Task ReleaseHeldItemsAsync()
    {
        var now = DateTime.UtcNow;
        var itemsToRelease = await _unitOfWork.InventoryItems.GetAllAsync(
            i => i.Status == InventoryItemStatus.OnHold &&
                 i.HoldUntil.HasValue &&
                 i.HoldUntil.Value <= now,
            i => i.Product); // Include Product để có thể lấy tên

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

    private async Task<TradeRequestDto> GetTradeRequestByIdAsync(Guid tradeRequestId)
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

    #region notifications

    private async Task NotifyItemReleased(InventoryItem item)
    {
        var cacheKey = $"noti:item_released:{item.UserId}:{item.Id}";
        if (await _cacheService.ExistsAsync(cacheKey)) return;

        await _notificationService.PushNotificationToUser(
            item.UserId,
            new NotificationDTO
            {
                Title = "Vật phẩm sẵn sàng trade!",
                Message = $"'{item.Product?.Name}' đã có thể trao đổi lại.",
                Type = NotificationType.System
            }
        );

        await _cacheService.SetAsync(cacheKey, true, TimeSpan.FromHours(1));
    }

    private async Task SendTradeRequestNotificationIfNotSentAsync(User user, string? requesterName)
    {
        var cacheKey = $"noti:trade_request:{user.Id}";
        if (await _cacheService.ExistsAsync(cacheKey)) return;

        var message = string.IsNullOrEmpty(requesterName)
            ? "Bạn có một yêu cầu trao đổi vật phẩm mới. Hãy kiểm tra và phản hồi sớm nhé!"
            : $"{requesterName} đã gửi yêu cầu trao đổi vật phẩm với bạn. Hãy kiểm tra ngay!";

        await _notificationService.PushNotificationToUser(
            user.Id,
            new NotificationDTO
            {
                Title = "Yêu cầu trao đổi mới!",
                Message = message,
                Type = NotificationType.Trading
            }
        );

        await _cacheService.SetAsync(cacheKey, true, TimeSpan.FromMinutes(2));
    }

    private async Task SendTradeResponseNotificationAsync(User requester, string responderName, bool isAccepted)
    {
        var cacheKey = $"noti:trade_response:{requester.Id}";
        if (await _cacheService.ExistsAsync(cacheKey)) return;

        var title = isAccepted ? "Yêu cầu trao đổi được chấp nhận!" : "Yêu cầu trao đổi bị từ chối";
        var message = isAccepted
            ? $"{responderName} đã chấp nhận yêu cầu trao đổi của bạn. Hãy liên hệ để hoàn tất giao dịch!"
            : $"{responderName} đã từ chối yêu cầu trao đổi của bạn.";

        await _notificationService.PushNotificationToUser(
            requester.Id,
            new NotificationDTO
            {
                Title = title,
                Message = message,
                Type = NotificationType.Trading
            }
        );

        await _cacheService.SetAsync(cacheKey, true, TimeSpan.FromMinutes(15));
    }

    private async Task SendRealTimeLockNotification(TradeRequest tradeRequest, Guid currentUserId, Guid listingOwnerId)
    {
        var otherUserId = currentUserId == listingOwnerId ? tradeRequest.RequesterId : listingOwnerId;

        await _notificationHub.Clients.User(otherUserId.ToString())
            .SendAsync("TradeRequestLocked", new
            {
                TradeRequestId = tradeRequest.Id,
                Message = "Đối tác đã lock giao dịch. Hãy kiểm tra!",
                OwnerLocked = tradeRequest.OwnerLocked,
                RequesterLocked = tradeRequest.RequesterLocked
            });
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
        {
            var cacheKey = $"noti:deal_locked:{noti.User.Id}";
            if (await _cacheService.ExistsAsync(cacheKey)) continue;

            await _notificationService.PushNotificationToUser(
                noti.User.Id,
                new NotificationDTO
                {
                    Title = "Giao dịch đã được khóa!",
                    Message = noti.Message,
                    Type = NotificationType.Trading
                }
            );

            await _cacheService.SetAsync(cacheKey, true, TimeSpan.FromHours(1));
        }
    }

    #endregion


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
            var remainingTime = heldItem.HoldUntil.Value - now;
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
            var listingDto = await _listingService.GetListingByIdAsync(listingId);
            _logger.Info($"[ValidateTradeRequestCreation] Đã tìm thấy listing {listingId} qua ListingService");

            // Sau khi xác nhận listing tồn tại, lấy thông tin đầy đủ từ database
            var listing = await _unitOfWork.Listings.GetByIdAsync(listingId,
                l => l.InventoryItem,
                l => l.InventoryItem.User);

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
            i => i.Product);

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

    private Task ProcessLockAndCompleteIfReady(TradeRequest tradeRequest, Guid userId, Guid listingOwnerId)
    {
        _logger.Info(
            $"[ProcessLockAndCompleteIfReady] Xử lý lock - TradeRequestId: {tradeRequest.Id}, UserId: {userId}, ListingOwnerId: {listingOwnerId}");
        _logger.Info(
            $"[ProcessLockAndCompleteIfReady] Current lock status - OwnerLocked: {tradeRequest.OwnerLocked}, RequesterLocked: {tradeRequest.RequesterLocked}");

        // Kiểm tra quyền lock
        var isOwner = userId == listingOwnerId;
        var isRequester = userId == tradeRequest.RequesterId;

        if (!isOwner && !isRequester)
        {
            _logger.Error(
                $"[ProcessLockAndCompleteIfReady] User {userId} không phải là owner ({listingOwnerId}) hoặc requester ({tradeRequest.RequesterId})");
            throw ErrorHelper.Forbidden("Bạn không có quyền lock giao dịch này.");
        }

        // Xác định user nào lock
        if (isOwner)
        {
            _logger.Info($"[ProcessLockAndCompleteIfReady] Owner (User A) {userId} thực hiện lock");
            // Kiểm tra đã lock chưa để tránh lock nhiều lần
            if (tradeRequest.OwnerLocked)
                _logger.Warn($"[ProcessLockAndCompleteIfReady] Owner {userId} đã lock trước đó");

            tradeRequest.OwnerLocked = true;
        }
        else // isRequester
        {
            _logger.Info($"[ProcessLockAndCompleteIfReady] Requester (User B) {userId} thực hiện lock");
            // Kiểm tra đã lock chưa để tránh lock nhiều lần
            if (tradeRequest.RequesterLocked)
                _logger.Warn($"[ProcessLockAndCompleteIfReady] Requester {userId} đã lock trước đó");

            tradeRequest.RequesterLocked = true;
        }

        _logger.Info(
            $"[ProcessLockAndCompleteIfReady] Trạng thái lock sau khi cập nhật - OwnerLocked: {tradeRequest.OwnerLocked}, RequesterLocked: {tradeRequest.RequesterLocked}");

        // Khi cả 2 đã lock - tự động hoàn thành giao dịch
        if (tradeRequest.OwnerLocked && tradeRequest.RequesterLocked)
            _logger.Info(
                $"[ProcessLockAndCompleteIfReady] Cả hai bên đã lock, bắt đầu hoàn thành giao dịch {tradeRequest.Id}");
        // Phần còn lại giữ nguyên...
        else
            _logger.Info("[ProcessLockAndCompleteIfReady] Chưa đủ điều kiện hoàn thành (cần cả hai bên lock)");
        return Task.CompletedTask;
    }

    private static TradeRequestDto MapTradeRequestToDto(TradeRequest tradeRequest, List<InventoryItem> offeredItems)
    {
        var listingItem = tradeRequest.Listing?.InventoryItem;
        var listingItemName = listingItem?.Product?.Name ?? "Unknown";

        // Gán giá trị mặc định cho tier nếu null
        var listingItemTier = listingItem?.Tier ?? RarityName.Common; // Giá trị mặc định
        var requesterName = tradeRequest.Requester?.FullName ?? "Unknown";

        var offeredItemDtos = offeredItems.Select(item => new OfferedItemDto
        {
            InventoryItemId = item.Id,
            ItemName = item.Product?.Name,
            ImageUrl = item.Product?.ImageUrls?.FirstOrDefault(),
            Tier = item.Tier ?? RarityName.Common // Giá trị mặc định
        }).ToList();

        var dto = new TradeRequestDto
        {
            Id = tradeRequest.Id,
            ListingId = tradeRequest.ListingId,
            ListingItemName = listingItemName,
            ListingItemTier = listingItemTier, // Luôn có giá trị (không null)
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