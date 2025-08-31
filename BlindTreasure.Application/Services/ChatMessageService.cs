using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ChatMessageService : IChatMessageService
{
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILoggerService _logger;
    private readonly Dictionary<string, DateTime> _onlineUsers = new();
    private readonly IUnitOfWork _unitOfWork;

    public ChatMessageService(ICacheService cacheService, IClaimsService claimsService, ILoggerService logger,
        IUnitOfWork unitOfWork, IHubContext<ChatHub> hubContext, IBlobService blobService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
        _blobService = blobService;
    }

    public async Task ShareInventoryItemAsync(Guid senderId, Guid receiverId, Guid inventoryItemId,
        string customMessage = "")
    {
        var receiver = await _unitOfWork.Users.GetByIdAsync(receiverId);
        if (receiver == null || receiver.IsDeleted)
            throw ErrorHelper.NotFound("Người nhận không tồn tại.");

        var inventoryItem = await _unitOfWork.InventoryItems.GetQueryable()
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == inventoryItemId);

        if (inventoryItem == null)
            throw ErrorHelper.NotFound("Vật phẩm không tồn tại.");

        if (inventoryItem.UserId != senderId)
            throw ErrorHelper.Forbidden("Bạn không có quyền chia sẻ vật phẩm này.");

        await SaveInventoryItemMessageAsync(senderId, receiverId, inventoryItemId, customMessage);

        var sender = await _unitOfWork.Users.GetByIdAsync(senderId);

        var itemDto = new InventoryItemDto
        {
            Id = inventoryItem.Id,
            UserId = inventoryItem.UserId,
            ProductId = inventoryItem.ProductId,
            ProductName = inventoryItem.Product?.Name ?? "Không xác định",
            Image = inventoryItem.Product?.ImageUrls?.FirstOrDefault() ?? "/assets/no-image.png",
            Location = inventoryItem.Location,
            Status = inventoryItem.Status,
            CreatedAt = inventoryItem.CreatedAt,
            IsFromBlindBox = inventoryItem.IsFromBlindBox,
            SourceCustomerBlindBoxId = inventoryItem.SourceCustomerBlindBoxId,
            Tier = inventoryItem.Tier,
            IsOnHold = inventoryItem.HoldUntil.HasValue && inventoryItem.HoldUntil > DateTime.UtcNow,
            HasActiveListing = inventoryItem.Listings != null &&
                               inventoryItem.Listings.Any(l => !l.IsDeleted && l.Status == ListingStatus.Active)
        };

        var content = string.IsNullOrEmpty(customMessage)
            ? $"[Chia sẻ vật phẩm: {inventoryItem.Product?.Name ?? "Không xác định"}]"
            : customMessage;

        var messageData = new
        {
            id = Guid.NewGuid().ToString(),
            senderId = senderId.ToString(),
            receiverId = receiverId.ToString(),
            senderName = sender?.FullName ?? "Unknown",
            senderAvatar = sender?.AvatarUrl ?? "",
            content,
            inventoryItemId = inventoryItem.Id,
            inventoryItem = itemDto,
            messageType = nameof(ChatMessageType.InventoryItemMessage),
            timestamp = DateTime.UtcNow,
            isRead = false
        };

        // Gửi qua SignalR
        await _hubContext.Clients.Users([senderId.ToString(), receiverId.ToString()])
            .SendAsync("ReceiveInventoryItemMessage", messageData);

        var unreadCount = await GetUnreadMessageCountAsync(receiverId);
        await _hubContext.Clients.User(receiverId.ToString()).SendAsync("UnreadCountUpdated", unreadCount);
    }


    public async Task SetUserOffline(string userId)
    {
        _onlineUsers.Remove(userId);
        await _cacheService.RemoveAsync($"user_online:{userId}");
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid userId)
    {
        return await _unitOfWork.ChatMessages.GetQueryable()
            .CountAsync(m => m.ReceiverId == userId && !m.IsRead);
    }

    /// <summary>
    ///     Upload và gửi tin nhắn hình ảnh hoặc video
    /// </summary>
    /// <param name="senderId">ID người gửi</param>
    /// <param name="receiverId">ID người nhận</param>
    /// <param name="mediaFile">File hình ảnh hoặc video</param>
    /// <returns>URL của file đã upload</returns>
    public async Task<string> UploadAndSendImageMessageAsync(Guid senderId, Guid receiverId, IFormFile mediaFile)
    {
        // Validate file
        if (mediaFile == null)
            throw ErrorHelper.BadRequest("Vui lòng chọn file media.");

        if (!IsValidMediaFile(mediaFile))
            throw ErrorHelper.BadRequest("File không hợp lệ. Vui lòng kiểm tra định dạng và kích thước file.");

        // Kiểm tra receiver có tồn tại không
        var receiver = await _unitOfWork.Users.GetByIdAsync(receiverId);
        if (receiver == null || receiver.IsDeleted)
            throw ErrorHelper.NotFound("Người nhận không tồn tại.");

        // Xác định loại media (hình ảnh hoặc video)
        var isVideo = mediaFile.ContentType.StartsWith("video/");
        var fileExtension = Path.GetExtension(mediaFile.FileName).ToLowerInvariant();

        // Tạo tên file duy nhất và thư mục lưu trữ
        var uniqueFileName = $"chat/{senderId}/{Guid.NewGuid()}{fileExtension}";

        // Upload media lên MinIO
        using var stream = mediaFile.OpenReadStream();
        await _blobService.UploadFileAsync(uniqueFileName, stream);

        // Lấy URL của media
        var mediaUrl = await _blobService.GetPreviewUrlAsync(uniqueFileName);

        // Tính kích thước file
        var fileSizeStr = FormatFileSize(mediaFile.Length);

        // Lưu tin nhắn media vào database
        var messageType = isVideo ? ChatMessageType.VideoMessage : ChatMessageType.ImageMessage;
        var content = isVideo ? "[Video]" : "[Hình ảnh]";

        // Gọi phương thức lưu tin nhắn media
        if (isVideo)
            // TODO: Nếu cần có phương thức riêng cho video thì implement thêm
            await SaveImageMessageAsync(senderId, receiverId, mediaUrl, mediaFile.FileName, fileSizeStr,
                mediaFile.ContentType);
        else
            await SaveImageMessageAsync(senderId, receiverId, mediaUrl, mediaFile.FileName, fileSizeStr,
                mediaFile.ContentType);

        // Lấy thông tin người gửi
        var sender = await _unitOfWork.Users.GetByIdAsync(senderId);

        // Tạo message object để gửi qua SignalR
        var messageData = new
        {
            id = Guid.NewGuid().ToString(),
            senderId = senderId.ToString(),
            receiverId = receiverId.ToString(),
            senderName = sender?.FullName ?? "Unknown",
            senderAvatar = sender?.AvatarUrl ?? "",
            content = mediaUrl, // Theo yêu cầu, gửi URL vào content
            fileName = mediaFile.FileName,
            fileSize = fileSizeStr,
            mimeType = mediaFile.ContentType,
            messageType = messageType.ToString(),
            timestamp = DateTime.UtcNow,
            isRead = false
        };

        // Gửi qua SignalR cho cả sender và receiver
        var eventName = isVideo ? "ReceiveVideoMessage" : "ReceiveImageMessage";
        await _hubContext.Clients.Users(new[] { senderId.ToString(), receiverId.ToString() })
            .SendAsync(eventName, messageData);

        // Cập nhật số tin chưa đọc cho receiver
        var unreadCount = await GetUnreadMessageCountAsync(receiverId);
        await _hubContext.Clients.User(receiverId.ToString()).SendAsync("UnreadCountUpdated", unreadCount);

        // Log hành động
        _logger.Info($"[Chat] User {senderId} sent {(isVideo ? "video" : "image")} to {receiverId}: {mediaUrl}");

        return mediaUrl;
    }


    /// <summary>
    ///     Lấy danh sách cuộc trò chuyện của user hiện tại với một user khác
    /// </summary>
    /// <param name="currentUserId">ID người gửi</param>
    /// <param name="receiverId">ID người nhận</param>
    /// <returns>ConversationDto</returns>
    public async Task<ConversationDto> GetNewConversationByReceiverIdAsync(Guid currentUserId, Guid receiverId)
    {
        _logger.Info(
            $"[GetNewConversationByReceiverIdAsync] User {currentUserId} requests conversation with receiver {receiverId}.");

        // Tạo cache key dựa trên các tham số
        var cacheKey = $"chat:conversations:by:{currentUserId}:{receiverId}";

        // ✅ GIẢM THỜI GIAN CACHE CHO CONVERSATION VÌ CẦN CẬP NHẬT ONLINE STATUS
        var cachedResult = await _cacheService.GetAsync<ConversationDto>(cacheKey);
        if (cachedResult != null)
        {
            // ✅ CẬP NHẬT LẠI TRẠNG THÁI ONLINE NGAY CẢ KHI CÓ CACHE
            cachedResult.IsOnline = await IsUserOnline(cachedResult.OtherUserId.ToString());

            _logger.Info($"[GetNewConversationByReceiverIdAsync] Cache hit for conversation with key: {cacheKey}");
            return cachedResult;
        }

        var otherUser = await _unitOfWork.Users.GetQueryable()
            .Where(u => u.Id == receiverId && !u.IsDeleted)
            .FirstOrDefaultAsync();

        var isSeller = false;
        var otherUserName = "Unknown";
        var otherUserAvatar = "";

        if (otherUser != null)
        {
            if (otherUser.RoleName == RoleType.Seller)
            {
                // Nếu user có role là Seller, lấy thông tin từ bảng Seller
                var seller = await _unitOfWork.Sellers.GetQueryable()
                    .Include(s => s.User)
                    .Where(s => s.UserId == receiverId)
                    .FirstOrDefaultAsync();

                if (seller != null)
                {
                    isSeller = true;
                    otherUserName = seller.CompanyName ?? "Unknown";
                    otherUserAvatar = seller.User.AvatarUrl ?? "";
                }
            }
            else
            {
                // Nếu không phải Seller, sử dụng thông tin User
                otherUserName = otherUser.FullName ?? "Unknown";
                otherUserAvatar = otherUser.AvatarUrl ?? "";
            }
        }

        // Lấy thông tin tin nhắn mới nhất (nếu có)
        var lastMessage = await _unitOfWork.ChatMessages.GetQueryable()
            .Where(m =>
                (m.SenderId == currentUserId && m.ReceiverId == receiverId) ||
                (m.SenderId == receiverId && m.ReceiverId == currentUserId &&
                 m.SenderType == ChatParticipantType.User &&
                 m.ReceiverType == ChatParticipantType.User))
            .OrderByDescending(m => m.SentAt)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        string lastMessageContent = null;
        DateTime? lastMessageTime = null;
        var unreadCount = 0;

        if (lastMessage != null)
        {
            // Xử lý nội dung tin nhắn
            lastMessageContent = lastMessage.Content;
            switch (lastMessage.MessageType)
            {
                case ChatMessageType.ImageMessage:
                    lastMessageContent = "[Hình ảnh]";
                    break;
                case ChatMessageType.VideoMessage:
                    lastMessageContent = "[Video]";
                    break;
                case ChatMessageType.InventoryItemMessage:
                    lastMessageContent = "[Chia sẻ vật phẩm]";
                    break;
                case ChatMessageType.AiToUser:
                case ChatMessageType.UserToAi:
                    lastMessageContent = lastMessage.Content;
                    break;
                default:
                    lastMessageContent = lastMessage.Content;
                    break;
            }

            lastMessageTime = lastMessage.SentAt;

            // Đếm số tin nhắn chưa đọc
            unreadCount = await _unitOfWork.ChatMessages.GetQueryable()
                .CountAsync(m => m.SenderId == receiverId &&
                                 m.ReceiverId == currentUserId &&
                                 !m.IsRead &&
                                 m.SenderType == ChatParticipantType.User &&
                                 m.ReceiverType == ChatParticipantType.User);
        }

        var conversation = new ConversationDto
        {
            OtherUserId = receiverId,
            OtherUserName = otherUserName,
            OtherUserAvatar = otherUserAvatar,
            LastMessage = lastMessageContent,
            LastMessageTime = lastMessageTime,
            UnreadCount = unreadCount,
            IsOnline = await IsUserOnline(receiverId.ToString()),
            IsSeller = isSeller
        };

        // ✅ CACHE NGẮN HƠN VÌ CẦN CẬP NHẬT ONLINE STATUS THƯỜNG XUYÊN
        await _cacheService.SetAsync(cacheKey, conversation, TimeSpan.FromSeconds(15));
        _logger.Info($"[GetNewConversationByReceiverIdAsync] Conversation cached with key: {cacheKey}");

        return conversation;
    }

    public async Task<ChatMessageDto?> GetMessageByIdAsync(Guid messageId)
    {
        var currentUserId = _claimsService.CurrentUserId;

        var message = await _unitOfWork.ChatMessages.GetQueryable()
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null) return null;

        return new ChatMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            SenderName = message.SenderType == ChatParticipantType.AI
                ? "BlindTreasure AI"
                : message.Sender?.FullName ?? "Unknown",
            SenderAvatar = message.SenderType == ChatParticipantType.AI
                ? "/assets/blindy-avatar.png" // Avatar mặc định cho AI
                : message.Sender?.AvatarUrl ?? "",
            Content = message.Content,
            SentAt = message.SentAt,
            IsRead = message.IsRead,
            IsCurrentUserSender = message.SenderId == currentUserId
        };
    }

    public async Task SetUserOnline(string userId)
    {
        _onlineUsers[userId] = DateTime.UtcNow;
        await _cacheService.SetAsync($"user_online:{userId}", DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    public async Task<bool> IsUserOnline(string userId)
    {
        // Kiểm tra cả trong memory và cache Redis
        if (_onlineUsers.ContainsKey(userId)) return true;

        return await _cacheService.ExistsAsync($"user_online:{userId}");
    }

    public async Task SaveMessageAsync(Guid senderId, Guid receiverId, string content)
    {
        var message = new ChatMessage
        {
            SenderId = senderId,
            SenderType = ChatParticipantType.User,
            ReceiverId = receiverId == Guid.Empty ? null : receiverId,
            ReceiverType = receiverId == Guid.Empty ? ChatParticipantType.AI : ChatParticipantType.User,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MessageType = receiverId == Guid.Empty ? ChatMessageType.UserToAi : ChatMessageType.UserToUser
        };


        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        var previewKey = GetLastMessageCacheKey(senderId, receiverId);
        await _cacheService.SetAsync(previewKey, message, TimeSpan.FromHours(1));

        _logger.Info($"[Chat] {senderId} → {receiverId}: {content}");
    }

    // Trong ChatMessageService.cs - phương thức GetMessagesAsync
    public async Task<Pagination<ChatMessageDto>> GetMessagesAsync(
        Guid currentUserId,
        Guid targetId,
        PaginationParameter param)
    {
        _logger.Info(
            $"[GetMessagesAsync] User {currentUserId} requests messages with {targetId}. Page: {param.PageIndex}, Size: {param.PageSize}");

        var cacheKey = $"chat:messages:{currentUserId}:{targetId}:{param.PageIndex}:{param.PageSize}";
        var cachedResult = await _cacheService.GetAsync<Pagination<ChatMessageDto>>(cacheKey);
        if (cachedResult != null)
        {
            _logger.Info($"[GetMessagesAsync] Cache hit for messages with key: {cacheKey}");
            return cachedResult;
        }

        IQueryable<ChatMessage> query;

        if (targetId == Guid.Empty)
            // Chat User ↔ AI
            query = _unitOfWork.ChatMessages.GetQueryable()
                .Where(m =>
                    (m.SenderType == ChatParticipantType.User && m.SenderId == currentUserId &&
                     m.ReceiverType == ChatParticipantType.AI)
                    ||
                    (m.SenderType == ChatParticipantType.AI && m.ReceiverType == ChatParticipantType.User &&
                     m.ReceiverId == currentUserId)
                );
        else
            // Chat User ↔ User
            query = _unitOfWork.ChatMessages.GetQueryable()
                .Where(m =>
                    (m.SenderId == currentUserId && m.ReceiverId == targetId &&
                     m.SenderType == ChatParticipantType.User && m.ReceiverType == ChatParticipantType.User)
                    ||
                    (m.SenderId == targetId && m.ReceiverId == currentUserId &&
                     m.SenderType == ChatParticipantType.User && m.ReceiverType == ChatParticipantType.User)
                );

        // ✅ Include đầy đủ InventoryItem quan hệ
        query = query
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Include(m => m.InventoryItem)
            .ThenInclude(i => i.Product)
            .Include(m => m.InventoryItem.Listings)
            .Include(m => m.InventoryItem.OrderDetail)
            .Include(m => m.InventoryItem.Shipment)
            .OrderBy(m => m.SentAt)
            .AsNoTracking();

        var count = await query.CountAsync();

        List<ChatMessage> messages;
        if (param.PageIndex == 0)
            messages = await query.ToListAsync();
        else
            messages = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var chatMessageDtos = messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            SenderName = m.SenderType == ChatParticipantType.AI
                ? "BlindTreasure AI"
                : m.Sender?.FullName ?? "Unknown",
            SenderAvatar = m.SenderType == ChatParticipantType.AI
                ? "/assets/blindy-avatar.png"
                : m.Sender?.AvatarUrl ?? "",
            Content = m.Content,
            SentAt = m.SentAt,
            IsRead = m.IsRead,
            IsCurrentUserSender = m.SenderId == currentUserId,
            MessageType = m.MessageType,
            FileUrl = m.FileUrl,
            FileName = m.FileName,
            FileSize = m.FileSize,
            FileMimeType = m.FileMimeType,
            InventoryItemId = m.InventoryItemId,
            InventoryItem = m.InventoryItem != null
                ? new InventoryItemDto
                {
                    Id = m.InventoryItem.Id,
                    UserId = m.InventoryItem.UserId,
                    ProductId = m.InventoryItem.ProductId,
                    ProductName = m.InventoryItem.Product?.Name ?? "Sản phẩm không xác định",
                    Image = m.InventoryItem.Product?.ImageUrls?.FirstOrDefault() ?? "/assets/no-image.png",
                    Location = m.InventoryItem.Location ?? "Không xác định",
                    Status = m.InventoryItem.Status,
                    CreatedAt = m.InventoryItem.CreatedAt,
                    IsFromBlindBox = m.InventoryItem.IsFromBlindBox,
                    SourceCustomerBlindBoxId = m.InventoryItem.SourceCustomerBlindBoxId,
                    Tier = m.InventoryItem.Tier,
                    IsOnHold = m.InventoryItem.HoldUntil.HasValue && m.InventoryItem.HoldUntil > DateTime.UtcNow,
                    HasActiveListing = m.InventoryItem.Listings != null &&
                                       m.InventoryItem.Listings.Any(l =>
                                           !l.IsDeleted && l.Status == ListingStatus.Active)
                }
                : null
        }).ToList();

        var result = new Pagination<ChatMessageDto>(chatMessageDtos, count, param.PageIndex, param.PageSize);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(30));
        _logger.Info($"[GetMessagesAsync] Messages cached with key: {cacheKey}");

        return result;
    }

    public async Task<Pagination<ConversationDto>> GetConversationsAsync(
        Guid userId,
        PaginationParameter param)
    {
        _logger.Info(
            $"[GetConversationsAsync] User {userId} requests conversations. Page: {param.PageIndex}, Size: {param.PageSize}");

        // Tạo cache key dựa trên các tham số
        var cacheKey = $"chat:conversations:{userId}:{param.PageIndex}:{param.PageSize}";

        // ✅ GIẢM THỜI GIAN CACHE CHO CONVERSATION VÌ CẦN CẬP NHẬT ONLINE STATUS
        var cachedResult = await _cacheService.GetAsync<Pagination<ConversationDto>>(cacheKey);
        if (cachedResult != null)
        {
            // ✅ CẬP NHẬT LẠI TRẠNG THÁI ONLINE NGAY CẢ KHI CÓ CACHE
            foreach (var conversation in cachedResult.ToList())
                conversation.IsOnline = await IsUserOnline(conversation.OtherUserId.ToString());

            _logger.Info($"[GetConversationsAsync] Cache hit for conversations with key: {cacheKey}");
            return cachedResult;
        }

        // Truy vấn cơ bản cho tất cả tin nhắn liên quan đến người dùng này
        var baseQuery = _unitOfWork.ChatMessages.GetQueryable()
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => (m.SenderId == userId || m.ReceiverId == userId) &&
                        m.SenderType == ChatParticipantType.User &&
                        m.ReceiverType == ChatParticipantType.User)
            .AsNoTracking();

        // Lấy danh sách ID của những người đã trò chuyện với user hiện tại
        var otherUserIds = await baseQuery
            .Select(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
            .Where(id => id != null)
            .Distinct()
            .ToListAsync();

        // Đếm tổng số cuộc hội thoại
        var count = otherUserIds.Count;

        // Phân trang danh sách người dùng
        var paginatedUserIds = otherUserIds;
        if (param.PageIndex > 0)
            paginatedUserIds = otherUserIds
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToList();
        else if (param.PageSize > 0 && otherUserIds.Count > param.PageSize)
            paginatedUserIds = otherUserIds.Take(param.PageSize).ToList();

        // Danh sách cuộc trò chuyện
        var conversations = new List<ConversationDto>();

        foreach (var otherUserId in paginatedUserIds)
        {
            if (otherUserId == null) continue;

            // Lấy tin nhắn mới nhất giữa 2 người
            var lastMessage = await baseQuery
                .Where(m =>
                    (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                    (m.SenderId == otherUserId && m.ReceiverId == userId))
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            if (lastMessage == null) continue;

            // Đếm số tin nhắn chưa đọc
            var unreadCount = await baseQuery
                .CountAsync(m => m.SenderId == otherUserId &&
                                 m.ReceiverId == userId &&
                                 !m.IsRead);

            // Xác định người dùng hoặc seller
            var isSeller = false;
            var otherUserName = "Unknown";
            var otherUserAvatar = "";

            // Kiểm tra trong bảng User trước
            User? otherUser = null;
            if (lastMessage.SenderId == otherUserId)
                otherUser = lastMessage.Sender;
            else
                otherUser = lastMessage.Receiver;

            if (otherUser != null)
            {
                if (otherUser.RoleName == RoleType.Seller)
                {
                    // Nếu user có role là Seller, lấy thông tin từ bảng Seller
                    var seller = await _unitOfWork.Sellers.GetQueryable()
                        .Include(s => s.User)
                        .Where(s => s.UserId == otherUserId)
                        .FirstOrDefaultAsync();
                    if (seller != null)
                    {
                        isSeller = true;
                        otherUserName = seller.CompanyName ?? "Unknown";
                        otherUserAvatar = seller.User.AvatarUrl ?? "";
                    }
                }
                else
                {
                    // Nếu không phải Seller, sử dụng thông tin User
                    otherUserName = otherUser.FullName ?? "Unknown";
                    otherUserAvatar = otherUser.AvatarUrl ?? "";
                }
            }
            else
            {
                var seller = await _unitOfWork.Sellers.GetQueryable()
                    .Include(s => s.User)
                    .Where(s => s.UserId == otherUserId)
                    .FirstOrDefaultAsync();
                if (seller != null)
                {
                    isSeller = true;
                    otherUserName = seller.CompanyName ?? "Unknown";
                    otherUserAvatar = seller.User.AvatarUrl ?? "";
                }
            }

            // ✅ CẢI THIỆN XỬ LÝ NỘI DUNG TIN NHẮN
            var lastMessageContent = lastMessage.Content;
            switch (lastMessage.MessageType)
            {
                case ChatMessageType.ImageMessage:
                    lastMessageContent = "[Hình ảnh]";
                    break;
                case ChatMessageType.VideoMessage:
                    lastMessageContent = "[Video]";
                    break;
                case ChatMessageType.InventoryItemMessage:
                    lastMessageContent = "[Chia sẻ vật phẩm]";
                    break;
                case ChatMessageType.AiToUser:
                case ChatMessageType.UserToAi:
                    lastMessageContent = lastMessage.Content;
                    break;
                default:
                    lastMessageContent = lastMessage.Content;
                    break;
            }

            var conversation = new ConversationDto
            {
                OtherUserId = otherUserId.Value,
                OtherUserName = otherUserName,
                OtherUserAvatar = otherUserAvatar,
                LastMessage = lastMessageContent,
                LastMessageTime = lastMessage.SentAt,
                UnreadCount = unreadCount,
                IsOnline = false, // Sẽ cập nhật sau
                IsSeller = isSeller
            };

            conversations.Add(conversation);
        }

        // Sắp xếp theo thời gian của tin nhắn mới nhất
        conversations = conversations.OrderByDescending(c => c.LastMessageTime).ToList();

        // ✅ KIỂM TRA TRẠNG THÁI ONLINE CHO TẤT CẢ USERS CÙNG LÚC
        var userStatusTasks = conversations.Select(async c =>
        {
            c.IsOnline = await IsUserOnline(c.OtherUserId.ToString());
            return c;
        });

        await Task.WhenAll(userStatusTasks);

        // Tạo kết quả phân trang
        var result = new Pagination<ConversationDto>(conversations, count, param.PageIndex, param.PageSize);

        // ✅ CACHE NGẮN HƠN VÌ CẦN CẬP NHẬT ONLINE STATUS THƯỜNG XUYÊN
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(15));
        _logger.Info($"[GetConversationsAsync] Conversations cached with key: {cacheKey}");

        return result;
    }

    public async Task MarkMessagesAsReadAsync(Guid fromUserId, Guid toUserId)
    {
        try
        {
            var unreadMessages = await _unitOfWork.ChatMessages.GetQueryable()
                .Where(m => m.SenderId == fromUserId &&
                            m.ReceiverId == toUserId &&
                            !m.IsRead)
                .ToListAsync();

            if (!unreadMessages.Any()) return;

            var now = DateTime.UtcNow;
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = now;
            }

            await _unitOfWork.ChatMessages.UpdateRange(unreadMessages);
            await _unitOfWork.SaveChangesAsync();

            // Gửi sự kiện SignalR cho sender và cập nhật UI
            await _hubContext.Clients.User(fromUserId.ToString()).SendAsync("MessageReadConfirmed", new
            {
                readerId = toUserId,
                messages = unreadMessages.Select(m => new
                {
                    m.Id,
                    m.ReadAt
                }).ToList()
            });

            // Thêm phần thông báo cập nhật UnreadCount
            var unreadCount = await GetUnreadMessageCountAsync(toUserId);
            await _hubContext.Clients.User(toUserId.ToString()).SendAsync("UnreadCountUpdated", unreadCount);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error marking messages as read: {ex.Message}");
            // Không throw exception để không làm gián đoạn luồng
        }
    }

    public async Task MarkConversationAsReadAsync(Guid currentUserId, Guid otherUserId)
    {
        try
        {
            var unreadMessages = await _unitOfWork.ChatMessages.GetQueryable()
                .Where(m => m.SenderId == otherUserId &&
                            m.ReceiverId == currentUserId &&
                            !m.IsRead)
                .ToListAsync();

            if (!unreadMessages.Any()) return;

            var now = DateTime.UtcNow;
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = now;
            }

            await _unitOfWork.ChatMessages.UpdateRange(unreadMessages);
            await _unitOfWork.SaveChangesAsync();

            // Thông báo cho người gửi biết tin nhắn đã được đọc
            await _hubContext.Clients.User(otherUserId.ToString()).SendAsync("ConversationRead", new
            {
                readerId = currentUserId,
                timestamp = now
            });

            // Cập nhật tổng số tin nhắn chưa đọc cho người dùng hiện tại
            var totalUnread = await GetUnreadMessageCountAsync(currentUserId);
            await _hubContext.Clients.User(currentUserId.ToString()).SendAsync("TotalUnreadUpdated", totalUnread);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error marking conversation as read: {ex.Message}");
        }
    }

    public async Task SaveAiMessageAsync(Guid userId, string content)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.NotFound("Người nhận không tồn tại.");

        var message = new ChatMessage
        {
            SenderId = null,
            SenderType = ChatParticipantType.AI,
            ReceiverId = userId,
            ReceiverType = ChatParticipantType.User,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MessageType = ChatMessageType.AiToUser
        };

        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();
    }


    public async Task SaveImageMessageAsync(Guid senderId, Guid receiverId,
        string imageUrl, string fileName, string fileSize, string mimeType)
    {
        var receiver = await _unitOfWork.Users.GetByIdAsync(receiverId);
        if (receiver == null || receiver.IsDeleted)
            throw ErrorHelper.NotFound("Người nhận không tồn tại.");

        var message = new ChatMessage
        {
            SenderId = senderId,
            SenderType = ChatParticipantType.User,
            ReceiverId = receiverId,
            ReceiverType = ChatParticipantType.User,
            Content = "[Hình ảnh]", // Nội dung mặc định cho ảnh
            FileUrl = imageUrl,
            FileName = fileName,
            FileSize = fileSize,
            FileMimeType = mimeType,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MessageType = ChatMessageType.ImageMessage
        };

        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        var previewKey = GetLastMessageCacheKey(senderId, receiverId);
        await _cacheService.SetAsync(previewKey, message, TimeSpan.FromHours(1));

        _logger.Info($"[Chat] {senderId} → {receiverId}: [Image: {imageUrl}]");
    }

    public async Task SaveInventoryItemMessageAsync(Guid senderId, Guid receiverId, Guid inventoryItemId,
        string customMessage = "")
    {
        var receiver = await _unitOfWork.Users.GetByIdAsync(receiverId);
        if (receiver == null || receiver.IsDeleted)
            throw ErrorHelper.NotFound("Người nhận không tồn tại.");

        var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(inventoryItemId);
        if (inventoryItem == null)
            throw ErrorHelper.NotFound("Vật phẩm không tồn tại.");

        if (inventoryItem.UserId != senderId)
            throw ErrorHelper.Forbidden("Bạn không có quyền chia sẻ vật phẩm này.");

        var content = string.IsNullOrEmpty(customMessage)
            ? $"[Chia sẻ vật phẩm: {inventoryItem.Product?.Name ?? "Không xác định"}]"
            : customMessage;

        var message = new ChatMessage
        {
            SenderId = senderId,
            SenderType = ChatParticipantType.User,
            ReceiverId = receiverId,
            ReceiverType = ChatParticipantType.User,
            Content = content,
            InventoryItemId = inventoryItemId,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MessageType = ChatMessageType.InventoryItemMessage
        };

        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        var previewKey = GetLastMessageCacheKey(senderId, receiverId);
        await _cacheService.SetAsync(previewKey, message, TimeSpan.FromHours(1));

        _logger.Info($"[Chat] {senderId} → {receiverId}: [InventoryItem: {inventoryItemId}]");
    }

    private static string GetLastMessageCacheKey(Guid user1Id, Guid user2Id)
    {
        var ids = new[] { user1Id, user2Id }.OrderBy(x => x).ToList();
        return $"chat:last:{ids[0]}:{ids[1]}";
    }

    private bool IsValidMediaFile(IFormFile file)
    {
        // Check if file is null or empty
        if (file.Length == 0)
        {
            _logger.Warn("Empty file detected");
            return false;
        }

        // Check file size (max 50MB)
        const int maxSizeBytes = 50 * 1024 * 1024; // 50MB
        if (file.Length > maxSizeBytes)
        {
            _logger.Warn(
                $"File {file.FileName} exceeds size limit: {file.Length} bytes (max: {maxSizeBytes})");
            return false;
        }

        // Check file extension
        var allowedExtensions = new[]
        {
            // Images
            ".jpg", ".jpeg", ".png", ".gif", ".webp",
            // Videos
            ".mp4", ".mov", ".avi", ".wmv", ".mkv"
        };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
        {
            _logger.Warn($"File {file.FileName} has invalid extension: {fileExtension}");
            return false;
        }

        // Check MIME type
        var allowedMimeTypes = new[]
        {
            // Images
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/gif",
            "image/webp",
            // Videos
            "video/mp4",
            "video/quicktime",
            "video/x-msvideo",
            "video/x-ms-wmv",
            "video/x-matroska"
        };

        if (string.IsNullOrEmpty(file.ContentType) ||
            !allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            _logger.Warn($"File {file.FileName} has invalid MIME type: {file.ContentType}");
            return false;
        }

        _logger.Info($"File {file.FileName} passed validation");
        return true;
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        var order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}