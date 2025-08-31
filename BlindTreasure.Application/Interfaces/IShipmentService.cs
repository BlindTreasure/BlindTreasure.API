using BlindTreasure.Domain.DTOs.ShipmentDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IShipmentService
{
    Task<ShipmentDto?> GetByIdAsync(Guid shipmentId);
    Task<List<ShipmentDto>> GetByOrderDetailIdAsync(Guid orderDetailId);
    Task<List<ShipmentDto>> GetMyShipmentsAsync(Guid? orderId = null, Guid? orderDetailId = null);
}