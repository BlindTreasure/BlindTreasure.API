namespace BlindTreasure.Domain.DTOs.TransactionDTOs;

public class TransactionDto
{
    //public Guid PaymentId { get; set; }
    //public Payment Payment { get; set; }
    public Guid Id { get; set; }

    public string Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ExternalRef { get; set; }
}