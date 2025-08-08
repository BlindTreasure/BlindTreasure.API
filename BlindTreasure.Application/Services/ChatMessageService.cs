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
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ChatMessageService : IChatMessageService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly Dictionary<string, DateTime> _onlineUsers = new();

    public ChatMessageService(ICacheService cacheService, IClaimsService claimsService, ILoggerService logger,
        IUnitOfWork unitOfWork, IHubContext<ChatHub> hubContext)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
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

    public async Task<Pagination<ChatMessageDto>> GetMessagesAsync(
        Guid currentUserId,
        Guid targetId,
        PaginationParameter param)
    {
        _logger.Info(
            $"[GetMessagesAsync] User {currentUserId} requests messages with {targetId}. Page: {param.PageIndex}, Size: {param.PageSize}");

        // Tạo cache key dựa trên các tham số
        var cacheKey = $"chat:messages:{currentUserId}:{targetId}:{param.PageIndex}:{param.PageSize}";

        // Thử lấy từ cache trước
        var cachedResult = await _cacheService.GetAsync<Pagination<ChatMessageDto>>(cacheKey);
        if (cachedResult != null)
        {
            _logger.Info($"[GetMessagesAsync] Cache hit for messages with key: {cacheKey}");
            return cachedResult;
        }

        IQueryable<ChatMessage> query;

        if (targetId == Guid.Empty)
            // Chat giữa User và AI
            query = _unitOfWork.ChatMessages.GetQueryable()
                .Where(m =>
                    (m.SenderType == ChatParticipantType.User && m.SenderId == currentUserId &&
                     m.ReceiverType == ChatParticipantType.AI)
                    ||
                    (m.SenderType == ChatParticipantType.AI && m.ReceiverType == ChatParticipantType.User &&
                     m.ReceiverId == currentUserId)
                );
        else
            // Chat giữa 2 người dùng
            query = _unitOfWork.ChatMessages.GetQueryable()
                .Where(m =>
                    (m.SenderId == currentUserId && m.ReceiverId == targetId &&
                     m.SenderType == ChatParticipantType.User && m.ReceiverType == ChatParticipantType.User)
                    ||
                    (m.SenderId == targetId && m.ReceiverId == currentUserId &&
                     m.SenderType == ChatParticipantType.User && m.ReceiverType == ChatParticipantType.User)
                );

        // Thêm Include để load thông tin User
        query = query.Include(m => m.Sender).Include(m => m.Receiver)
            .OrderBy(m => m.SentAt)
            .AsNoTracking();

        // Tính tổng số lượng tin nhắn
        var count = await query.CountAsync();

        // Lấy tin nhắn theo phân trang
        List<ChatMessage> messages;
        if (param.PageIndex == 0)
            messages = await query.ToListAsync();
        else
            messages = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        // Map kết quả sang DTO
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
            // Thêm thông tin ảnh
            FileUrl = m.FileUrl,
            FileName = m.FileName,
            FileSize = m.FileSize,
            FileMimeType = m.FileMimeType,
            // Thêm thông tin InventoryItem
            InventoryItemId = m.InventoryItemId,
            InventoryItem = m.InventoryItem != null ? new InventoryItemDto
            {
                Id = m.InventoryItem.Id,
                ProductName = m.InventoryItem.Product?.Name ?? "Không xác định",
                Image = m.InventoryItem.Product?.ImageUrls.FirstOrDefault()!,
                Tier = m.InventoryItem.Tier,
                Status = m.InventoryItem.Status,
                Location = m.InventoryItem.Location
            } : null
        }).ToList();

        // Tạo kết quả phân trang
        var result = new Pagination<ChatMessageDto>(chatMessageDtos, count, param.PageIndex, param.PageSize);

        // Lưu kết quả vào cache với thời gian hết hạn ngắn (1 phút) vì chat thường xuyên cập nhật
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(1));
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

        // Thử lấy từ cache trước
        var cachedResult = await _cacheService.GetAsync<Pagination<ConversationDto>>(cacheKey);
        if (cachedResult != null)
        {
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

            // Xác định người dùng khác
            User? otherUser = null;
            if (lastMessage.SenderId == otherUserId)
                otherUser = lastMessage.Sender;
            else
                otherUser = lastMessage.Receiver;

            // Xử lý nội dung tin nhắn dựa trên loại tin nhắn
            string lastMessageContent = lastMessage.Content;
            if (lastMessage.MessageType == ChatMessageType.ImageMessage)
                lastMessageContent = "[Hình ảnh]";
            else if (lastMessage.MessageType == ChatMessageType.InventoryItemMessage)
                lastMessageContent = "[Chia sẻ vật phẩm]";

            var conversation = new ConversationDto
            {
                OtherUserId = otherUserId.Value,
                OtherUserName = otherUser?.FullName ?? "Unknown",
                OtherUserAvatar = otherUser?.AvatarUrl ?? "",
                LastMessage = lastMessageContent,
                LastMessageTime = lastMessage.SentAt,
                UnreadCount = unreadCount,
                IsOnline = false // Sẽ cập nhật sau
            };

            conversations.Add(conversation);
        }

        // Sắp xếp theo thời gian của tin nhắn mới nhất
        conversations = conversations.OrderByDescending(c => c.LastMessageTime).ToList();

        // Kiểm tra trạng thái online
        foreach (var conversation in conversations)
            conversation.IsOnline = await IsUserOnline(conversation.OtherUserId.ToString());

        // Tạo kết quả phân trang
        var result = new Pagination<ConversationDto>(conversations, count, param.PageIndex, param.PageSize);

        // Lưu kết quả vào cache
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(30));
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

    private static string GetLastMessageCacheKey(Guid user1Id, Guid user2Id)
    {
        var ids = new[] { user1Id, user2Id }.OrderBy(x => x).ToList();
        return $"chat:last:{ids[0]}:{ids[1]}";
    }
}