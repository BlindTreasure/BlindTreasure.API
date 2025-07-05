using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UnboxDTOs;

public class UnboxResultDto
{
    public Guid ProductId { get; set; }
    // public BlindBoxRarity Rarity { get; set; }
    public DateTime UnboxedAt { get; set; }
}