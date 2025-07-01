using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Services;
using Microsoft.AspNetCore.SignalR;

namespace BlindTreasure.Application.SignalR.Hubs;

public class StaffChatHub : Hub
{
    private readonly IBlindyService _blindyService;

    public StaffChatHub(IBlindyService blindyService)
    {
        _blindyService = blindyService;
    }

    public async Task SendMessage(string userId, string message)
    {
        var normalized = message.Trim().ToLower();

        if (_blindyService is BlindyService impl)
            switch (normalized)
            {
                case "/analyze_users":
                    var userResult = await impl.AnalyzeUsersWithAi();
                    await Clients.Caller.SendAsync("ReceiveMessage", new
                    {
                        from = "ai",
                        content = userResult,
                        sentAt = DateTime.UtcNow
                    });
                    return;

                case "/help":
                    await Clients.Caller.SendAsync("ReceiveMessage", new
                    {
                        from = "ai",
                        content = """
                                  Các lệnh bạn có thể dùng:
                                  - /analyze users → Phân tích người dùng
                                  - /analyze sellers → Phân tích seller
                                  - /check orders → Thống kê đơn hàng
                                  - /help → Danh sách lệnh
                                  """,
                        sentAt = DateTime.UtcNow
                    });
                    return;
                // mở rộng các lệnh khác tại đây
            }

        // Nếu không phải lệnh → xử lý như chat thông thường
        var reply = await _blindyService.AskStaffAsync(message);
        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            from = "ai",
            content = reply,
            sentAt = DateTime.UtcNow
        });
    }
}