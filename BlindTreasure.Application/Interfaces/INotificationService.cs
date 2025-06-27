namespace BlindTreasure.Application.Interfaces;


public interface INotificationService
{
    Task SendWelcomeNotificationAsync(Guid userId);
}