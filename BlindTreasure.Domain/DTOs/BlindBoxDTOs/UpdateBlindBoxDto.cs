﻿using System.ComponentModel;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class UpdateBlindBoxDto
{
    [DefaultValue("Hộp blindbox cho GunDam")]
    public string? Name { get; set; }

    [DefaultValue(100000)] public decimal? Price { get; set; }

    [DefaultValue(100)] public int? TotalQuantity { get; set; }

    [DefaultValue("780e2631-c6e8-40fe-b487-09565faefffe")]
    public Guid? CategoryId { get; set; }

    [DefaultValue(typeof(DateTime), "2025-07-01T00:00:00")]
    public DateTime? ReleaseDate { get; set; }

    [DefaultValue("Mô tả cho blind box")] public string? Description { get; set; }

    public IFormFile? ImageFile { get; set; }

    [DefaultValue(false)] public bool? HasSecretItem { get; set; }

    [DefaultValue(5)] public int? SecretProbability { get; set; }
}