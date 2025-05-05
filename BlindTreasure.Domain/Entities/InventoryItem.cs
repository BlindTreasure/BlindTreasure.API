namespace BlindTreasure.Domain.Entities;

public class InventoryItem : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int RestockThreshold { get; set; }
    public string Location { get; set; }
    public string Status { get; set; }
}