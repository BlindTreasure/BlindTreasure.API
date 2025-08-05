using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs;

public class NotificationDto
{
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required NotificationType Type { get; set; }
    public string? SourceUrl { get; set; }

}