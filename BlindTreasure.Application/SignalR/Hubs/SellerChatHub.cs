using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class SellerChatHub : Hub
{
    private readonly IBlindyService _blindyService;

    public SellerChatHub(IBlindyService blindyService)
    {
        _blindyService = blindyService;
    }

    public async Task SendMessage(string userId, string message)
    {
        var reply = await _blindyService.AskSellerAsync(message);

        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            from = "ai",
            content = reply,
            sentAt = DateTime.UtcNow
        });
    }
}