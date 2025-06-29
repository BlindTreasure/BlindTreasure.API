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

    //additional properties for transaction details
    public string? Notes { get; set; } = ""; // Optional field for notes related to the subscription

    public DateTime? CompleteAt { get; set; } // Timestamp for when the transaction was completed
    public decimal? RefundAmount { get; set; }
    public string? StripeTransactionId { get; set; } // Discount rate (percentage) applied to the subscription
    //public string? StripeSessionId { get; set; } // Discount rate (percentage) applied to the subscription


}