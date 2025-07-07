using System.ComponentModel;
using System.Text.Json.Serialization;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class BlindBoxItemResponseDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public RarityName Rarity { get; set; }
    public int Weight { get; set; }
    public decimal DropRate { get; set; }
}


public class BlindBoxItemRequestDto
{
    public Guid ProductId { get; set; }
    public RarityName Rarity { get; set; }
    public int Quantity { get; set; }
    public int Weight { get; set; }
}
