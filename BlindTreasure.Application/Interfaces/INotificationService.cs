namespace BlindTreasure.Application.Interfaces;

public interface INotificationService
{
    Task SendWelcomeNotificationAsync(string userEmail);
}