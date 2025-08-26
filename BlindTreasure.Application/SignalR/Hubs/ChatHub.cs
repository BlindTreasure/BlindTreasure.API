using BlindTreasure.Application.Interfaces;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.SignalR.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IBlindyService _blindyService;
    private readonly IAdminService _userService;
    private readonly ICacheService _cacheService;
    private readonly IBlobService _blobService;
    private readonly IUnitOfWork _unitOfWork;

    public ChatHub(IChatMessageService chatMessageService, IBlindyService blindyService, IAdminService userService,
        ICacheService cacheService, IBlobService blobService, IUnitOfWork unitOfWork)
    {
        _chatMessageService = chatMessageService;
        _blindyService = blindyService;
        _userService = userService;
        _cacheService = cacheService;
        _blobService = blobService;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Đánh dấu toàn bộ tin nhắn trong cuộc trò chuyện là đã đọc
    /// </summary>
    /// <param name="otherUserId">ID của người gửi tin nhắn</param>
    /// <returns>Task</returns>
    public async Task MarkConversationAsRead(string otherUserId)
    {
        try
        {
            var currentUserId = Context.UserIdentifier;
            if (currentUserId == null || string.IsNullOrEmpty(otherUserId)) return;

            if (Guid.TryParse(otherUserId, out var otherUserGuid) &&
                Guid.TryParse(currentUserId, out var currentUserGuid))
            {
                await _chatMessageService.MarkConversationAsReadAsync(currentUserGuid, otherUserGuid);

                // Xóa cache liên quan đến tin nhắn giữa hai người dùng
                await InvalidateMessageCache(currentUserGuid, otherUserGuid);

                // Thông báo cho client biết đã đọc thành công
                await Clients.Caller.SendAsync("ConversationReadSuccess", new
                {
                    otherUserId,
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error",
                new { message = "Không thể đánh dấu tin đã đọc", details = ex.Message });
        }
    }

    /// <summary>
    /// Xử lý khi client kết nối tới SignalR hub
    /// Thêm user vào group và cập nhật trạng thái online
    /// </summary>
    /// <returns>Task</returns>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            await _chatMessageService.SetUserOnline(userId);

            await Clients.Others.SendAsync("UserOnline", new
            {
                userId,
                timestamp = DateTime.UtcNow
            });

            // Thông báo số lượng tin nhắn chưa đọc cho user khi kết nối
            if (Guid.TryParse(userId, out var userGuid))
            {
                var unreadCount = await _chatMessageService.GetUnreadMessageCountAsync(userGuid);
                await Clients.Caller.SendAsync("UnreadCountUpdated", unreadCount);
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Đánh dấu tin nhắn đã được xem bởi người nhận
    /// </summary>
    /// <param name="messageId">ID của tin nhắn</param>
    /// <param name="senderId">ID của người gửi</param>
    /// <returns>Task</returns>
    public async Task MarkMessageAsSeen(string messageId, string senderId)
    {
        try
        {
            var currentUserId = Context.UserIdentifier;
            if (currentUserId == null) return;

            if (Guid.TryParse(messageId, out var msgId) &&
                Guid.TryParse(senderId, out var senderGuid) &&
                Guid.TryParse(currentUserId, out var currentUserGuid))
            {
                await _chatMessageService.MarkMessagesAsReadAsync(senderGuid, currentUserGuid);

                // Xóa cache liên quan đến tin nhắn
                await InvalidateMessageCache(currentUserGuid, senderGuid);

                // Gửi event về sender để update UI (message đã được seen)
                await Clients.User(senderId).SendAsync("MessageSeen", new
                {
                    messageId,
                    seenBy = currentUserId,
                    seenAt = DateTime.UtcNow
                });

                // Gửi event về người nhận để cập nhật UI
                await Clients.Caller.SendAsync("MessageMarkedAsSeen", new
                {
                    messageId,
                    senderId,
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("SeenError", new { error = ex.Message });
        }
    }

    /// <summary>
    /// Xử lý khi client ngắt kết nối khỏi SignalR hub
    /// Xóa user khỏi group và thông báo trạng thái offline
    /// </summary>
    /// <param name="exception">Exception gây ra việc disconnect (nếu có)</param>
    /// <returns>Task</returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            await _chatMessageService.SetUserOffline(userId);

            await Clients.Others.SendAsync("UserOffline", new
            {
                userId,
                timestamp = DateTime.UtcNow
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Kiểm tra trạng thái online của một user cụ thể
    /// </summary>
    /// <param name="targetUserId">ID của user cần kiểm tra</param>
    /// <returns>Task</returns>
    public async Task CheckUserOnlineStatus(string targetUserId)
    {
        try
        {
            var isOnline = await _chatMessageService.IsUserOnline(targetUserId);
            await Clients.Caller.SendAsync("UserOnlineStatus", new
            {
                userId = targetUserId,
                isOnline,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("OnlineStatusError", new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gửi tin nhắn từ user hiện tại tới user khác
    /// </summary>
    /// <param name="receiverId">ID của người nhận</param>
    /// <param name="content">Nội dung tin nhắn</param>
    /// <returns>Task</returns>
    public async Task SendMessage(string receiverId, string content)
    {
        try
        {
            var senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(content.Trim()))
            {
                await Clients.Caller.SendAsync("MessageError", new { error = "Dữ liệu không hợp lệ" });
                return;
            }

            if (!Guid.TryParse(receiverId, out var receiverGuid))
            {
                await Clients.Caller.SendAsync("MessageError", new { error = "ID người nhận không hợp lệ" });
                return;
            }

            var senderGuid = Guid.Parse(senderId);

            // Kiểm tra receiver có tồn tại không
            var receiver = await _userService.GetUserById(receiverGuid);
            if (receiver == null || receiver.IsDeleted)
            {
                await Clients.Caller.SendAsync("MessageError", new { error = "Người nhận không tồn tại" });
                return;
            }

            await _chatMessageService.SaveMessageAsync(senderGuid, receiverGuid, content.Trim());

            // Xóa cache liên quan đến tin nhắn và cuộc trò chuyện
            await InvalidateMessageCache(senderGuid, receiverGuid);
            await InvalidateConversationCache(senderGuid);
            await InvalidateConversationCache(receiverGuid);

            // Gửi cho cả sender và receiver
            var message = new
            {
                id = Guid.NewGuid().ToString(), // Tạo ID tạm thời cho client
                senderId,
                receiverId,
                content = content.Trim(),
                isRead = false,
                timestamp = DateTime.UtcNow
            };

            await Clients.Users(new[] { senderId, receiverId }).SendAsync("ReceiveMessage", message);

            // Cập nhật số tin chưa đọc cho người nhận
            if (Guid.TryParse(receiverId, out var recvGuid))
            {
                var unreadCount = await _chatMessageService.GetUnreadMessageCountAsync(recvGuid);
                await Clients.User(receiverId).SendAsync("UnreadCountUpdated", unreadCount);
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("MessageError", new
            {
                error = "Không thể gửi tin nhắn",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Thông báo rằng user đang soạn tin nhắn (typing indicator)
    /// </summary>
    /// <param name="receiverId">ID của người nhận sẽ thấy typing indicator</param>
    /// <returns>Task</returns>
    public async Task StartTyping(string receiverId)
    {
        var senderId = Context.UserIdentifier;
        if (senderId != null && Guid.TryParse(receiverId, out _))
            await Clients.User(receiverId).SendAsync("UserStartedTyping", senderId);
    }

    /// <summary>
    /// Thông báo rằng user đã ngừng soạn tin nhắn (stop typing indicator)
    /// </summary>
    /// <param name="receiverId">ID của người nhận sẽ ngừng thấy typing indicator</param>
    /// <returns>Task</returns>
    public async Task StopTyping(string receiverId)
    {
        var senderId = Context.UserIdentifier;
        if (senderId != null && Guid.TryParse(receiverId, out _))
            await Clients.User(receiverId).SendAsync("UserStoppedTyping", senderId);
    }

    /// <summary>
    /// Xóa cache liên quan đến tin nhắn giữa hai người dùng
    /// </summary>
    /// <param name="user1Id">ID của người dùng 1</param>
    /// <param name="user2Id">ID của người dùng 2</param>
    /// <returns>Task</returns>
    private async Task InvalidateMessageCache(Guid user1Id, Guid user2Id)
    {
        // Xóa cache tin nhắn theo cả hai chiều
        await _cacheService.RemoveByPatternAsync($"chat:messages:{user1Id}:{user2Id}:*");
        await _cacheService.RemoveByPatternAsync($"chat:messages:{user2Id}:{user1Id}:*");

        // Xóa cache tin nhắn cuối cùng
        var lastMsgKey = GetLastMessageCacheKey(user1Id, user2Id);
        await _cacheService.RemoveAsync(lastMsgKey);
    }

    /// <summary>
    /// Xóa cache liên quan đến danh sách cuộc trò chuyện của người dùng
    /// </summary>
    /// <param name="userId">ID của người dùng</param>
    /// <returns>Task</returns>
    private async Task InvalidateConversationCache(Guid userId)
    {
        await _cacheService.RemoveByPatternAsync($"chat:conversations:{userId}:*");
    }

    /// <summary>
    /// Tạo key cache cho tin nhắn cuối cùng
    /// </summary>
    /// <param name="user1Id">ID của người dùng 1</param>
    /// <param name="user2Id">ID của người dùng 2</param>
    /// <returns>String</returns>
    private static string GetLastMessageCacheKey(Guid user1Id, Guid user2Id)
    {
        var ids = new[] { user1Id, user2Id }.OrderBy(x => x).ToList();
        return $"chat:last:{ids[0]}:{ids[1]}";
    }

    #region AI ko liên quan

    /// <summary>
    /// Gửi tin nhắn tới AI Blindy và nhận phản hồi
    /// </summary>
    /// <param name="prompt">Câu hỏi/prompt gửi tới AI</param>
    /// <returns>Task</returns>
    public async Task SendMessageToAi(string prompt)
    {
        var senderId = Context.UserIdentifier;
        if (senderId == null) return;

        var senderGuid = Guid.Parse(senderId);

        // Lưu câu hỏi của user
        await _chatMessageService.SaveMessageAsync(senderGuid, Guid.Empty, prompt); // User → AI
        var reply = await _blindyService.AskUserAsync(prompt);

        // Lưu câu trả lời của AI
        await _chatMessageService.SaveAiMessageAsync(senderGuid, reply); // AI → User

        // Xóa cache liên quan đến tin nhắn AI
        await _cacheService.RemoveByPatternAsync($"chat:messages:{senderGuid}:*");

        // Gửi về client 2 chiều
        await Clients.User(senderId).SendAsync("ReceiveMessage", new
        {
            senderId,
            receiverId = "AI",
            content = prompt,
            timestamp = DateTime.UtcNow
        });

        await Clients.User(senderId).SendAsync("ReceiveMessage", new
        {
            senderId = "AI",
            receiverId = senderId,
            content = reply,
            timestamp = DateTime.UtcNow
        });
    }

    #endregion
}