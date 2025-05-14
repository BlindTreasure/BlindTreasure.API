namespace BlindTreasure.Domain.Entities;

public class Promotion : BaseEntity
{
    public string Code { get; set; }
    public string Description { get; set; }
    public string DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UsageLimit { get; set; }
    public string Status { get; set; }
}