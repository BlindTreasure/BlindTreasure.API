using System.Reactive;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Hubs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Notification = BlindTreasure.Domain.Entities.Notification;

namespace BlindTreasure.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IUserService _userService;
    private readonly ICurrentTime _currentTime;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(ICacheService cacheService, IUnitOfWork unitOfWork, ICurrentTime currentTime,
        IHubContext<NotificationHub> hubContext, IUserService userService)
    {
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _currentTime = currentTime;
        _hubContext = hubContext;
        _userService = userService;
    }

    public async Task SendWelcomeNotificationAsync(string userEmail)
    {
        var cacheKey = $"noti:welcome:{userEmail}";

        // Check nếu đã gửi gần đây
        if (await _cacheService.ExistsAsync(cacheKey))
            return;

        var now = _currentTime.GetCurrentTime();
        var user = await _userService.GetUserByEmail(userEmail);
        if (user != null)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Type = NotificationType.System,
                Title = "Chào mừng!",
                Message = $"Chào mừng {user?.FullName} đến với BlindTreasure.",
                IsRead = false,
                SentAt = now,
                CreatedAt = now,
                CreatedBy = user.Id
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            var payload = new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.SentAt,
                notification.Type
            };

            await NotificationHub.SendToUser(_hubContext, userEmail, payload);
        }

        // Đánh dấu đã gửi, thời gian giữ key là 1 giờ (hoặc tuỳ chỉnh)
        await _cacheService.SetAsync(cacheKey, true, TimeSpan.FromSeconds(2));
    }
}