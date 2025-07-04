using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR;

public class UserIdProviderByGuid : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var id = connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Console.WriteLine($"[SignalR] Connected with userId: {id ?? "NULL"}");
        return id;
    }
}