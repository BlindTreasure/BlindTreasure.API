namespace BlindTreasure.Domain.Entities;

public class CustomerDiscount : BaseEntity
{
    // FK → User
    public Guid CustomerId { get; set; }
    public User Customer { get; set; }

    public decimal DiscountRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; }
}