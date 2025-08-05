using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Application.Interfaces;

public interface INotificationService
{
    Task<int> GetUnreadNotificationsCount(Guid userId);
    Task<Notification> ReadNotification(Guid notificationId);
    Task ReadAllNotifications(Guid userId);
    Task DeleteNotification(Guid notificationId);

    Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, int pageIndex, int pageSize,
        NotificationType? type = null);

    Task<int> CountNotificationsAsync(Guid userId);


    // Push notification
    Task<Notification> PushNotificationToAll(NotificationDto notificationDTO);
    Task<Notification> PushNotificationToUser(Guid userId, NotificationDto notificationDTO);
    Task<Notification> PushNotificationToRole(RoleType role, NotificationDto notificationDTO);
}