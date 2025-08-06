using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Notification : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public required NotificationType Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? SourceUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}