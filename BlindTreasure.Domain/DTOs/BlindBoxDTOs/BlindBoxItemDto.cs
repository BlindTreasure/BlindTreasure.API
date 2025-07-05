using System.ComponentModel;
using System.Text.Json.Serialization;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class BlindBoxItemDto
{
    public Guid ProductId { get; set; }

    public Guid RarityId { get; set; } // mới
    
    [JsonIgnore] public string? ProductName { get; set; }

    [DefaultValue("10")] public int Quantity { get; set; }

    [DefaultValue("10")] public decimal DropRate { get; set; }

    [JsonIgnore] public string? ImageUrl { get; set; }
}