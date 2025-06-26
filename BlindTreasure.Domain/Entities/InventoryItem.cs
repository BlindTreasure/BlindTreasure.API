namespace BlindTreasure.Domain.Entities;

public class InventoryItem : BaseEntity
{
    // FK → User (chủ sở hữu sau unbox)
    public Guid UserId { get; set; }
    public User User { get; set; }

    // FK → Product
    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; } = 0; // Số lượng đã được đặt trước, mặc định là 0
    public int RestockThreshold { get; set; } = 0; // Ngưỡng tồn kho để cảnh báo restock, mặc định là 0
    public string Location { get; set; } = "HCM"; // Vị trí kho, mặc định là "HCM"
    public string Status { get; set; }

    // 1-n → Listings
    public ICollection<Listing> Listings { get; set; }
}