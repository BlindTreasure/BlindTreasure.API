using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Mappers;

public static class InventoryItemMapper
{
    public static InventoryItemDto ToInventoryItemDto(InventoryItem item)
    {
        return new InventoryItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            ProductId = item.ProductId,
            ProductName = item.Product?.Name ?? string.Empty,
            ProductImages = item.Product?.ImageUrls ?? new List<string>(),
            Quantity = item.Quantity,
            ReservedQuantity = item.ReservedQuantity,
            RestockThreshold = item.RestockThreshold,
            Location = item.Location,
            Status = item.Status,
            CreatedAt = item.CreatedAt
        };
    }
}