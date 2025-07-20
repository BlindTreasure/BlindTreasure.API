using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces;

public interface IShipmentService
{
    Task<ShipmentDto?> GetByIdAsync(Guid shipmentId);
    Task<List<ShipmentDto>> GetByOrderDetailIdAsync(Guid orderDetailId);
    Task<List<ShipmentDto>> GetMyShipmentsAsync(Guid? orderId = null, Guid? orderDetailId = null);
}