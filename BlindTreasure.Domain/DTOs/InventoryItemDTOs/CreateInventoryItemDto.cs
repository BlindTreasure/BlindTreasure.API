﻿using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class CreateInventoryItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Location { get; set; }
    public InventoryItemStatus Status { get; set; } // enum
    public Guid? AddressId { get; set; } // FK → Address, optional

    // FK → OrderDetail
    public Guid? OrderDetailId { get; set; }

    // FK → Shipment
    public Guid? ShipmentId { get; set; }
}