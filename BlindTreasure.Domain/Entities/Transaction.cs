namespace BlindTreasure.Domain.Entities;

public class Transaction : BaseEntity
{
    // FK → Payment
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; }

    public string Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ExternalRef { get; set; }
}