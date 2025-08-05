using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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

    public async Task<List<Notification>> GetNotificationsAsync(Guid userId, int pageIndex, int pageSize,
        NotificationType? type = null)
    {
        var query = _unitOfWork.Notifications.GetQueryable()
            .Where(n => n.UserId == userId && !n.IsDeleted);

        if (type.HasValue) query = query.Where(n => n.Type == type.Value);

        return await query
            .OrderByDescending(n => n.SentAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountNotificationsAsync(Guid userId)
    {
        return await _unitOfWork.Notifications.GetQueryable()
            .CountAsync(n => n.UserId == userId && !n.IsDeleted);
    }


    public async Task<int> GetUnreadNotificationsCount(Guid userId)
    {
        return await _unitOfWork.Notifications.GetQueryable()
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDeleted);
    }

    public async Task<Notification> ReadNotification(Guid notificationId)
    {
        var notification = await _unitOfWork.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);
        if (notification == null) throw new Exception("Notification not found");
        notification.IsRead = true;
        notification.ReadAt = _currentTime.GetCurrentTime();
        await _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync();
        return notification;
    }

    public async Task ReadAllNotifications(Guid userId)
    {
        var notifications = await _unitOfWork.Notifications.GetQueryable()
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
            .ToListAsync();
        foreach (var n in notifications)
        {
            n.IsRead = true;
            n.ReadAt = _currentTime.GetCurrentTime();
            await _unitOfWork.Notifications.Update(n);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteNotification(Guid notificationId)
    {
        var notification = await _unitOfWork.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);
        if (notification == null) throw new Exception("Notification not found");
        notification.IsDeleted = true;
        notification.DeletedAt = _currentTime.GetCurrentTime();
        await _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<Notification> PushNotificationToAll(NotificationDto notificationDTO)
    {
        var users = await _unitOfWork.Users.GetQueryable().Where(u => !u.IsDeleted).ToListAsync();
        Notification? lastNotification = null;
        foreach (var user in users)
        {
            var notification = await PushNotificationToUser(user.Id, notificationDTO);
            lastNotification = notification;
        }

        return lastNotification!;
    }

    public async Task<Notification> PushNotificationToUser(Guid userId, NotificationDto notificationDTO)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new Exception("User not found");
        var now = _currentTime.GetCurrentTime();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = notificationDTO.Title,
            Message = notificationDTO.Message,
            Type = notificationDTO.Type,
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
        return notification;
    }

    public async Task<Notification> PushNotificationToRole(RoleType role, NotificationDto notificationDTO)
    {
        var users = await _unitOfWork.Users.GetQueryable().Where(u => u.RoleName == role && !u.IsDeleted).ToListAsync();
        Notification? lastNotification = null;
        foreach (var user in users)
        {
            var notification = await PushNotificationToUser(user.Id, notificationDTO);
            lastNotification = notification;
        }

        return lastNotification!;
    }
}