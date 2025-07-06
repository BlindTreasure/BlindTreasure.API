using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UnboxDTOs;

public class UnboxResultDto
{
    public Guid ProductId { get; set; }
    public RarityName? Rarity { get; set; }
    public int Weight { get; set; }
    public DateTime UnboxedAt { get; set; }
}