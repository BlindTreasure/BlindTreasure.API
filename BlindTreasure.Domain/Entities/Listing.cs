﻿using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Listing : BaseEntity
{
    // FK → InventoryItem
    public Guid InventoryId { get; set; }
    public InventoryItem InventoryItem { get; set; }

    public decimal Price { get; set; }
    public DateTime ListedAt { get; set; }
    public ListingStatus Status { get; set; }
}