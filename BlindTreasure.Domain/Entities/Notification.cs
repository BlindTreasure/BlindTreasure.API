namespace BlindTreasure.Domain.Entities;

public class Notification : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string Type { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}