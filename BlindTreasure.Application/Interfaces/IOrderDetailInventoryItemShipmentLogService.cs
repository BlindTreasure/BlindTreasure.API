using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces;

public interface IOrderDetailInventoryItemShipmentLogService
{
    Task<string> GenerateTrackingMessageAsync(Shipment shipment, ShipmentStatus oldStatus, ShipmentStatus newStatus,
        Seller seller, Address customerAddress);

    Task<List<OrderDetailInventoryItemShipmentLogDto>> GetLogByInventoryItemIdAsync(Guid inventoryItemId);
    Task<List<OrderDetailInventoryItemShipmentLogDto>> GetLogByOrderDetailIdAsync(Guid orderDetailId);
    Task<List<OrderDetailInventoryItemShipmentLogDto>> GetLogForShipmentByIdAsync(Guid shipmentId);

    Task<OrderDetailInventoryItemShipmentLog> LogInventoryItemOrCustomerBlindboxAddedAsync(OrderDetail orderDetail,
        InventoryItem? inventoryItem, CustomerBlindBox? blindBox, string? msg);

    Task<OrderDetailInventoryItemShipmentLog> LogOrderDetailCreationAsync(OrderDetail orderDetail, string? msg);

    Task<OrderDetailInventoryItemShipmentLog> LogOrderDetailStatusChangeAsync(OrderDetail orderDetail,
        OrderDetailItemStatus oldStatus, OrderDetailItemStatus newStatus, string? msg);

    Task<OrderDetailInventoryItemShipmentLog> LogShipmentAddedAsync(OrderDetail orderDetail, Shipment shipment,
        string? msg);

    Task<OrderDetailInventoryItemShipmentLog> LogShipmentOfOrderDetailChangedStatusAsync(OrderDetail orderDetail,
        ShipmentStatus oldStatus, Shipment shipmentNewStatus, string? msg);

    Task<OrderDetailInventoryItemShipmentLog> LogShipmentTrackingInventoryItemUpdateAsync(OrderDetail orderDetail,
        InventoryItemStatus oldStatus, InventoryItem InventoryWithNewStatus, string trackingMessage);
}