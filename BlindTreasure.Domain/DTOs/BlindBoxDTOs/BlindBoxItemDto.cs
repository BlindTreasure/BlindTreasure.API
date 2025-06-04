using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class BlindBoxItemDto
{
    public Guid ProductId { get; set; }

    // Bỏ ProductName, backend sẽ lấy dựa vào ProductId
     public string? ProductName { get; set; }

    public int Quantity { get; set; }
    public decimal DropRate { get; set; }
    public BlindBoxRarity Rarity { get; set; }

    public bool IsSecret { get; set; }  // Thêm trường này để detect secret item
}
