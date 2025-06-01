using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

public class GuestChatHub : Hub
{
    private readonly IBlindyService _blindyService;

    public GuestChatHub(IBlindyService blindyService)
    {
        _blindyService = blindyService;
    }

    public async Task SendMessage(string userId, string message)
    {
        var reply = await _blindyService.AskGuestAsync(message);

        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            from = "ai",
            content = reply,
            sentAt = DateTime.UtcNow
        });
    }
}