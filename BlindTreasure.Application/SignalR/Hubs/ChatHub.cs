using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IBlindyService _blindyService;
    private readonly IUserService _userService;


    public ChatHub(IChatMessageService chatMessageService, IBlindyService blindyService, IUserService userService)
    {
        _chatMessageService = chatMessageService;
        _blindyService = blindyService;
        _userService = userService;
    }

    // Cần thêm vào ChatHub
    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
    }

    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            await _chatMessageService.SetUserOnline(userId);

            // ❌ THIẾU: Thông báo online status
            await Clients.Others.SendAsync("UserOnline", new
            {
                userId,
                timestamp = DateTime.UtcNow
            });
        }

        await base.OnConnectedAsync();
    }


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

    public async Task StartTyping(string receiverId)
    {
        var senderId = Context.UserIdentifier;
        if (senderId != null && Guid.TryParse(receiverId, out _))
            await Clients.User(receiverId).SendAsync("UserStartedTyping", senderId);
    }


    public async Task StopTyping(string receiverId)
    {
        var senderId = Context.UserIdentifier;
        if (senderId != null && Guid.TryParse(receiverId, out _))
            await Clients.User(receiverId).SendAsync("UserStoppedTyping", senderId);
    }

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
}