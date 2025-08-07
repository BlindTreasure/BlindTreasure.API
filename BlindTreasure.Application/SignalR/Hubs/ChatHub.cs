using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IBlindyService _blindyService;
    private readonly IUserService _userService;

    /// <summary>
    /// Khởi tạo ChatHub với các dependencies cần thiết
    /// </summary>
    /// <param name="chatMessageService">Service xử lý tin nhắn chat</param>
    /// <param name="blindyService">Service xử lý AI Blindy</param>
    /// <param name="userService">Service xử lý người dùng</param>
    public ChatHub(IChatMessageService chatMessageService, IBlindyService blindyService, IUserService userService)
    {
        _chatMessageService = chatMessageService;
        _blindyService = blindyService;
        _userService = userService;
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

            if (Guid.TryParse(messageId, out var msgId) && Guid.TryParse(senderId, out var senderGuid))
            {
                await _chatMessageService.MarkMessagesAsReadAsync(senderGuid, Guid.Parse(currentUserId));

                // Gửi event về sender để update UI (message đã được seen)
                await Clients.User(senderId).SendAsync("MessageSeen", new
                {
                    messageId,
                    seenBy = currentUserId,
                    seenAt = DateTime.UtcNow
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

            // Gửi cho cả sender và receiver
            await Clients.Users(new[] { senderId, receiverId }).SendAsync("ReceiveMessage", new
            {
                senderId,
                receiverId,
                content = content.Trim(),
                timestamp = DateTime.UtcNow
            });
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