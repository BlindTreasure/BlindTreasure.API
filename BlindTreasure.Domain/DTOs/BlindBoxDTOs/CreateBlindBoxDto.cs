using System.ComponentModel;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class CreateBlindBoxDto
{
    [DefaultValue("Hộp blindbox cho GunDam")]
    public required string Name { get; set; }

    [DefaultValue(100000)] public decimal Price { get; set; }

    [DefaultValue(100)] public int TotalQuantity { get; set; }

    [DefaultValue(typeof(DateTime), "2025-07-01T00:00:00")]
    public DateTime ReleaseDate { get; set; }

    [DefaultValue("Mô tả cho blind box")] public required string Description { get; set; }

    public required IFormFile ImageFile { get; set; }

    [DefaultValue(false)] public bool HasSecretItem { get; set; } = false;

    [DefaultValue(5)] public int SecretProbability { get; set; }
}