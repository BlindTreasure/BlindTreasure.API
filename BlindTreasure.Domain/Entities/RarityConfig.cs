namespace BlindTreasure.Domain.Entities;

public class RarityConfig : BaseEntity
{
    public required string Name { get; set; } // Ví dụ: "Common", "Rare", "Secret"
    public decimal Weight { get; set; }       // Trọng số dùng để tính DropRate
    public bool IsSecret { get; set; }        // Đánh dấu nếu là loại bí mật
    
    public ICollection<BlindBoxItem>? BlindBoxItems { get; set; }
}
