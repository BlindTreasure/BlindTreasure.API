namespace BlindTreasure.Domain.Entities;

public class VerificationRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string RequestType { get; set; }
    public string Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public User ReviewedByUser { get; set; }
    public string RejectionReason { get; set; }
}