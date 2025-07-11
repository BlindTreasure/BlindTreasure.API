using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageService _chatMessageService;

    public ChatHub(IChatMessageService chatMessageService)
    {
        _chatMessageService = chatMessageService;
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
}