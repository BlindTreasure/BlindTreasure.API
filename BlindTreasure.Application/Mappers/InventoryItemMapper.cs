using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.Entities;

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
            //ProductName = item.Product?.Name ?? string.Empty,
            //ProductImages = item.Product?.ImageUrls ?? new List<string>(),
            Location = item.Location,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            Product = item.Product != null ? ProductDtoMapper.ToProducDetailDto(item.Product) : null,
            IsFromBlindBox = item.IsFromBlindBox,
            SourceCustomerBlindBoxId = item.SourceCustomerBlindBoxId,
            OrderDetailId = item.OrderDetailId,
            //OrderDetail = item.OrderDetail != null ? OrderDtoMapper.ToOrderDetailDto(item.OrderDetail) : null,
            ShipmentId = item.ShipmentId,
            //Shipment = item.Shipment != null ? ShipmentDtoMapper.ToShipmentDto(item.Shipment) : null
        };
    }

    public static InventoryItemDto ToInventoryItemDtoFullIncluded(InventoryItem item)
    {
       var result = ToInventoryItemDto(item);
        result.OrderDetail = item.OrderDetail != null ? OrderDtoMapper.ToOrderDetailDto(item.OrderDetail) : null;
        result.Shipment = item.Shipment != null ? ShipmentDtoMapper.ToShipmentDto(item.Shipment) : null;
        result.Product = item.Product != null ? ProductDtoMapper.ToProducDetailDto(item.Product) : null;

        return result;
    }
}