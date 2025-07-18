﻿using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Application.Interfaces;

public interface INotificationService
{
    Task<int> GetUnreadNotificationsCount(Guid userId);
    Task<Notification> ReadNotification(Guid notificationId);
    Task ReadAllNotifications(Guid userId);
    Task DeleteNotification(Guid notificationId);

    Task<List<Notification>> GetNotificationsAsync(Guid userId, int pageIndex, int pageSize);
    Task<int> CountNotificationsAsync(Guid userId);


    // Push notification
    Task<Notification> PushNotificationToAll(NotificationDTO notificationDTO);
    Task<Notification> PushNotificationToUser(Guid userId, NotificationDTO notificationDTO);
    Task<Notification> PushNotificationToRole(RoleType role, NotificationDTO notificationDTO);
}