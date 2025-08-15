using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces;

public interface IOrderDetailInventoryItemLogService
{
    Task<string> GenerateTrackingMessageAsync(Shipment shipment, ShipmentStatus oldStatus, ShipmentStatus newStatus,
        Seller seller, Address customerAddress);

    Task<List<OrderDetailInventoryItemLogDto>> GetLogByInventoryItemIdAsync(Guid inventoryItemId);
    Task<List<OrderDetailInventoryItemLogDto>> GetLogByOrderDetailIdAsync(Guid orderDetailId);

    Task<OrderDetailInventoryItemLog> LogInventoryItemOrCustomerBlindboxAddedAsync(OrderDetail orderDetail,
        InventoryItem? inventoryItem, CustomerBlindBox? blindBox, string? msg);

    Task<OrderDetailInventoryItemLog> LogOrderDetailCreationAsync(OrderDetail orderDetail, string? msg);

    Task<OrderDetailInventoryItemLog> LogOrderDetailStatusChangeAsync(OrderDetail orderDetail,
        OrderDetailItemStatus oldStatus, OrderDetailItemStatus newStatus, string? msg);

    Task<OrderDetailInventoryItemLog> LogShipmentAddedAsync(OrderDetail orderDetail, Shipment shipment, string? msg);

    Task<OrderDetailInventoryItemLog> LogShipmentOfOrderDetailChangedStatusAsync(OrderDetail orderDetail,
        ShipmentStatus oldStatus, Shipment shipmentNewStatus, string? msg);

    Task<OrderDetailInventoryItemLog> LogShipmentTrackingInventoryItemUpdateAsync(OrderDetail orderDetail,
        InventoryItemStatus oldStatus, InventoryItem InventoryWithNewStatus, string trackingMessage);
}