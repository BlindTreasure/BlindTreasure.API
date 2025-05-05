namespace BlindTreasure.Domain.Entities;

public class SupportTicket : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string Subject { get; set; }
    public string Description { get; set; }
    public string Status { get; set; }
    public string Priority { get; set; }
    public Guid? AssignedTo { get; set; }
    public User AssignedToUser { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string Resolution { get; set; }
}