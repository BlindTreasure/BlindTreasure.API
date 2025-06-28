using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    public static async Task SendToUser(IHubContext<NotificationHub> hubContext, string userEmail, object payload)
    {
        await hubContext.Clients.User(userEmail).SendAsync("ReceiveNotification", payload);
    }
}