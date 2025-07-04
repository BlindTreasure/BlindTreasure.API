using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Notification = BlindTreasure.Domain.Entities.Notification;

namespace BlindTreasure.Application.Services;

public class NotificationService : INotificationService
{
    private readonly ICacheService _cacheService;
    private readonly ICurrentTime _currentTime;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserService _userService;

    public NotificationService(ICacheService cacheService, IUnitOfWork unitOfWork, ICurrentTime currentTime,
        IHubContext<NotificationHub> hubContext, IUserService userService)
    {
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _currentTime = currentTime;
        _hubContext = hubContext;
        _userService = userService;
    }

    public async Task SendNotificationToUserAsync(Guid userId, string title, string message, NotificationType type,
        TimeSpan? cooldown = null)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return;

        // var cacheKey = $"noti:{type}:{user.Email}";
        // if (cooldown.HasValue && await _cacheService.ExistsAsync(cacheKey))
        //     return;

        var now = _currentTime.GetCurrentTime();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = title,
            Message = message,
            Type = type,
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

        await NotificationHub.SendToUser(_hubContext, user.Id.ToString(), payload);

        // if (cooldown.HasValue)
        //     await _cacheService.SetAsync(cacheKey, true, cooldown.Value);
    }
}