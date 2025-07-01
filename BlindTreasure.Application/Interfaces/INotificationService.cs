using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Application.Interfaces;

public interface INotificationService
{
    Task SendNotificationToUserAsync(Guid userId, string title, string message, NotificationType type,
        TimeSpan? cooldown = null);
}