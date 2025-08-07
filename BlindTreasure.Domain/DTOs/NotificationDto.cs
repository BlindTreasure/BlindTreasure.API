using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; } // Thêm Id
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required NotificationType Type { get; set; }
    public string? SourceUrl { get; set; }
    public bool IsRead { get; set; } // Thêm IsRead
    public DateTime SentAt { get; set; } // Thêm SentAt
    public DateTime? ReadAt { get; set; } // Thêm ReadAt
}