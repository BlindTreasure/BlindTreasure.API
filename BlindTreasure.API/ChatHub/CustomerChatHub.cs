using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.API.ChatHub;

public class CustomerChatHub : Hub
{
    private readonly IBlindyService _blindyService;

    public CustomerChatHub(IBlindyService blindyService)
    {
        _blindyService = blindyService;
    }

    public async Task SendMessage(string userId, string message)
    {
        var reply = await _blindyService.AskCustomerAsync(message);

        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            from = "ai",
            content = reply,
            sentAt = DateTime.UtcNow
        });
    }
}
