namespace BlindTreasure.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; }

    public decimal Amount { get; set; }
    public string Method { get; set; }
    public string Status { get; set; }
    public string TransactionId { get; set; }
    public DateTime PaidAt { get; set; }
    public decimal? RefundedAmount { get; set; }

    public ICollection<Transaction> Transactions { get; set; }
}