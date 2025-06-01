using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.API.ChatHub;

public class UserChatHub : Hub
{
    private readonly IBlindyService _blindyService;

    public UserChatHub(IBlindyService blindyService)
    {
        _blindyService = blindyService;
    }

    public async Task SendMessage(string userId, string message)
    {
        var reply = await _blindyService.AskUserAsync(message);

        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            from = "ai",
            content = reply,
            sentAt = DateTime.UtcNow
        });
    }
}