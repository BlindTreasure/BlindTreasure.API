using BlindTreasure.Domain.DTOs.UnboxLogDTOs;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class UnboxingHub : Hub
{
    public async Task SendUnboxingNotification(UnboxLogDto unboxLog)
    {
        // Gửi thông báo đến tất cả client
        await Clients.All.SendAsync("ReceiveUnboxingNotification", unboxLog);
    }
}