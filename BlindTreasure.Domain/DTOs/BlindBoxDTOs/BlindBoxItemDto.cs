using System.ComponentModel;
using System.Text.Json.Serialization;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class BlindBoxItemDto
{
    public Guid ProductId { get; set; }
    public RarityName Rarity { get; set; } // enum: Common, Rare, Epic, Secret
    [DefaultValue("10")] public int Quantity { get; set; }

    [DefaultValue("10")] public int Weight { get; set; }

    [JsonIgnore] public string? ProductName { get; set; }
    [JsonIgnore] public string? ImageUrl { get; set; }
    [JsonIgnore] public decimal? DropRate { get; set; }
}