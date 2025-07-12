using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IBlindyService _blindyService;


    public ChatHub(IChatMessageService chatMessageService, IBlindyService blindyService)
    {
        _chatMessageService = chatMessageService;
        _blindyService = blindyService;
    }

    public async Task SendMessage(string receiverId, string content)
    {
        var senderId = Context.UserIdentifier;
        if (Guid.TryParse(receiverId, out var receiverGuid))
            if (senderId != null)
            {
                await _chatMessageService.SaveMessageAsync(Guid.Parse(senderId), receiverGuid, content);
                await Clients.User(receiverId).SendAsync("ReceiveMessage", new
                {
                    senderId,
                    receiverId,
                    content,
                    timestamp = DateTime.UtcNow
                });
            }
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