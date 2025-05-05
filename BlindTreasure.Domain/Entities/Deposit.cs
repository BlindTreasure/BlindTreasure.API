namespace BlindTreasure.Domain.Entities;

public class Deposit : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public DateTime DepositDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Status { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public Guid? ReleasedBy { get; set; }
    public User ReleasedByUser { get; set; }
}