using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Mappers;

public static class ShipmentDtoMapper
{
    public static ShipmentDto ToShipmentDto(Shipment shipment)
    {
        if (shipment == null)
            throw ErrorHelper.Internal("Dữ liệu shipment không hợp lệ.");

        return new ShipmentDto
        {
            Id = shipment.Id,
            //OrderDetail = shipment.,
            OrderCode = shipment.OrderCode,
            TotalFee = shipment.TotalFee,
            MainServiceFee = shipment.MainServiceFee,
            Provider = shipment.Provider,
            TrackingNumber = shipment.TrackingNumber,
            ShippedAt = shipment.ShippedAt,
            EstimatedDelivery = shipment.EstimatedDelivery,
            DeliveredAt = shipment.DeliveredAt,
            Status = shipment.Status,

        };
    }

    public static ShipmentDto ToShipmentDtoWithFullIncluded(Shipment shipment)
    {
        if (shipment == null)
            throw ErrorHelper.Internal("Dữ liệu shipment không hợp lệ.");

        var result = ToShipmentDto(shipment);
        result.InventoryItems = shipment.InventoryItems?.Select(InventoryItemMapper.ToInventoryItemDto).ToList() ??
                                new List<InventoryItemDto>();
        result.OrderDetails = shipment.OrderDetails?.Select(OrderDtoMapper.ToOrderDetailDto).ToList() ??
                                new List<OrderDetailDto>();

        return result;
    }
}