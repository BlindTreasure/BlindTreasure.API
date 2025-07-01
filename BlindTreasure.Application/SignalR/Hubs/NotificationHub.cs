using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class NotificationHub : Hub
{
    public static async Task SendToUser(IHubContext<NotificationHub> hubContext, string userId, object payload)
    {
        Console.WriteLine($"[Hub] Gửi noti đến userId={userId}, payload={JsonSerializer.Serialize(payload)}");
        await hubContext.Clients.User(userId).SendAsync("ReceiveNotification", payload);
    }


}