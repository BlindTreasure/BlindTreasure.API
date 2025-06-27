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
    private readonly ICurrentTime _currentTime;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(ICacheService cacheService, IUnitOfWork unitOfWork, ICurrentTime currentTime,
        IHubContext<NotificationHub> hubContext)
    {
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _currentTime = currentTime;
        _hubContext = hubContext;
    }

    public async Task SendWelcomeNotificationAsync(Guid userId)
    {
        var now = _currentTime.GetCurrentTime();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.System,
            Title = "Chào mừng!",
            Message = "Chào mừng bạn đến với BlindTreasure.",
            IsRead = false,
            SentAt = now,
            CreatedAt = now,
            CreatedBy = userId
        };

        await _unitOfWork.Notifications.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();

        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
        {
            notification.Id,
            notification.Title,
            notification.Message,
            notification.SentAt,
            notification.Type
        });
    }
}