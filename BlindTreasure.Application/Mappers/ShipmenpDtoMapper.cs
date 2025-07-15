using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Mappers
{
    public static class ShipmentDtoMapper
    {
        public static ShipmentDto ToShipmentDto(Shipment shipment)
        {
            if (shipment == null)
                throw ErrorHelper.Internal("Dữ liệu shipment không hợp lệ.");

            return new ShipmentDto
            {
                OrderDetail = shipment.OrderDetail != null ? OrderDtoMapper.ToOrderDetailDto(shipment.OrderDetail) : null,
                OrderCode = shipment.OrderCode,
                TotalFee = shipment.TotalFee,
                MainServiceFee = shipment.MainServiceFee,
                Provider = shipment.Provider,
                TrackingNumber = shipment.TrackingNumber,
                ShippedAt = shipment.ShippedAt,
                EstimatedDelivery = shipment.EstimatedDelivery,
                DeliveredAt = shipment.DeliveredAt,
                Status = shipment.Status
            };
        }
    }
}
