using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services
{
    public class OrderDetailInventoryItemLogService : IOrderDetailInventoryItemLogService
    {
        private readonly ICacheService _cacheService;
        private readonly IClaimsService _claimsService;
        private readonly IInventoryItemService _inventoryItemService;
        private readonly ILoggerService _logger;
        private readonly IUnitOfWork _unitOfWork;

        public OrderDetailInventoryItemLogService(ICacheService cacheService, IClaimsService claimsService,  IInventoryItemService inventoryItemService, ILoggerService logger, IUnitOfWork unitOfWork)
        {
            _cacheService = cacheService;
            _claimsService = claimsService;
            _inventoryItemService = inventoryItemService;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        // Implement methods for OrderDetailInventoryItemLogService here
        public async Task<OrderDetailInventoryItemLog> LogOrderDetailCreationAsync(OrderDetail orderDetail, string? msg)
        {
            var log = new OrderDetailInventoryItemLog
            {
                OrderDetailId = orderDetail.Id,
                ActionType = ActionType.ORDER_DETAIL_CREATED,
                LogContent = msg ?? $"Order detail created for {(orderDetail.ProductId.HasValue ? "product" : "blindbox")}",
                OldValue = null,
                NewValue = orderDetail.Status.ToString(),
                ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null
            };

            if (log.ActorId == null)
            {
                _logger.Warn("This update made by system");
                log.LogContent += " (System action)";
            }

            var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
            //await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<OrderDetailInventoryItemLog> LogShipmentAddedAsync(
            OrderDetail orderDetail,
            Shipment shipment, string? msg)
        {
            var log = new OrderDetailInventoryItemLog
            {
                OrderDetailId = orderDetail.Id,
                ActionType = ActionType.SHIPMENT_ADDED,
                LogContent = msg ?? $"Shipment added: {shipment.Id} for this order-detail",
                OldValue = null,
                NewValue = shipment.Status.ToString(),
                ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null
            };

            if (log.ActorId == null)
            {
                _logger.Warn("This update made by system");
                log.LogContent += " (System action)";
            }

            var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
            //await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<OrderDetailInventoryItemLog> LogOrderDetailStatusChangeAsync(
            OrderDetail orderDetail,
            OrderDetailItemStatus oldStatus,
            OrderDetailItemStatus newStatus, string? msg)
        {
            var log = new OrderDetailInventoryItemLog
            {
                OrderDetailId = orderDetail.Id,
                ActionType = ActionType.ORDER_DETAIL_STATUS_CHANGED,
                LogContent = msg ?? $"Order detail status updated, become to: {newStatus}",
                OldValue = oldStatus.ToString(),
                NewValue = newStatus.ToString(),
                ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null
            };

            if (log.ActorId == null)
            {
                _logger.Warn("This update made by system");
                log.LogContent += " (System action)";
            }

            var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
            //await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<OrderDetailInventoryItemLog> LogInventoryItemOrCustomerBlindboxAddedAsync(
            OrderDetail orderDetail,
            InventoryItem? inventoryItem,
            CustomerBlindBox? blindBox, string? msg)
        {
            try
            {
                var log = new OrderDetailInventoryItemLog
                {
                    OrderDetailId = orderDetail.Id,
                    InventoryItemId = inventoryItem?.Id,
                    ActionType = inventoryItem != null ? ActionType.INVENTORY_ITEM_ADDED : ActionType.BLIND_BOX_ADDED,
                    LogContent = msg ?? (inventoryItem != null
                    ? $"Inventory item created: {inventoryItem.Id}"
                    : $"Blind box added: {blindBox?.BlindBoxId}"),
                    OldValue = null,
                    NewValue = inventoryItem != null ? inventoryItem.Status.ToString() : "UNOPENED",
                    ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null
                };

                if (log.ActorId == null)
                {
                    _logger.Warn("This update made by system");
                    log.LogContent += " (System action)";
                }

                var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
                //await _unitOfWork.SaveChangesAsync();
                return result;
            }
            catch (Exception ex) 
            {
                _logger.Error("Error: " + ex.Message);
                throw ErrorHelper.BadRequest(ex.Message);
            }
        }

        public async Task<OrderDetailInventoryItemLog> LogShipmentTrackingInventoryItemUpdateAsync(
            InventoryItem inventoryItem,
            InventoryItemStatus oldStatus,
            InventoryItem shipmentWithNewStatus,
            string trackingMessage)
        {
            if (inventoryItem.OrderDetailId == null)
            {
                _logger.Warn($"InventoryItem {inventoryItem.Id} is not linked to any OrderDetail. Cannot log tracking update.");
                return null!;
            }

            var log = new OrderDetailInventoryItemLog
            {
                OrderDetailId = inventoryItem.OrderDetailId.Value,
                InventoryItemId = inventoryItem.Id,
                ActionType = ActionType.SHIPMENT_STATUS_CHANGED,
                LogContent = trackingMessage,
                OldValue = oldStatus.ToString(),
                NewValue = shipmentWithNewStatus.Status.ToString(),
                ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
                LogTime = DateTime.UtcNow
            };

            if (log.ActorId == null)
            {
                _logger.Warn("This update made by system");
                log.LogContent += " (System action)";
            }

            var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
            //await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<string> GenerateTrackingMessageAsync(
        Shipment shipment,
        ShipmentStatus oldStatus,
        ShipmentStatus newStatus,
        Seller seller,
        Address customerAddress)
    {
        try
        {
            var message = new StringBuilder();
            message.Append($"Shipment {shipment.OrderCode}: ");

            switch (newStatus)
            {
                case ShipmentStatus.PROCESSING:
                    message.Append($"Shipment requested. Pickup scheduled at {shipment.EstimatedPickupTime:yyyy-MM-dd HH:mm} ");
                    message.Append($"from {seller?.CompanyAddress}.");
                    break;

                case ShipmentStatus.PICKED_UP:
                    message.Append($"Item picked up from seller at {seller?.CompanyAddress}. ");
                    message.Append($"Estimated delivery: {shipment.EstimatedDelivery:yyyy-MM-dd}.");
                    break;

                case ShipmentStatus.IN_TRANSIT:
                    message.Append($"Item in transit by GHN, estimated Delivery {shipment.EstimatedDelivery:yyyy-MM-dd}.");
                    break;

                case ShipmentStatus.DELIVERED:
                    message.Append($"Delivered to customer at {customerAddress?.AddressLine} ");
                    message.Append($"on {DateTime.UtcNow:yyyy-MM-dd HH:mm}.");
                    break;

                default:
                    message.Append($"Status changed from {oldStatus} to {newStatus}.");
                    break;
            }

            return message.ToString();
        }
        catch (Exception ex)
        {
            _logger.Error($"GenerateTrackingMessage error: {ex.Message}");
            return $"Shipment update: {oldStatus} → {newStatus}";
    }
    
}
        /// <summary>
        /// Lấy danh sách log theo OrderDetailId.
        /// </summary>
        public async Task<List<OrderDetailInventoryItemLogDto>> GetLogByOrderDetailIdAsync(Guid orderDetailId)
        {
            var logs = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
                .Where(l => l.OrderDetailId == orderDetailId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return logs.Select(InventoryItemMapper.ToOrderDetailInventoryItemLogDto).ToList();
        }

        /// <summary>
        /// Lấy danh sách log theo InventoryItemId.
        /// </summary>
        public async Task<List<OrderDetailInventoryItemLogDto>> GetLogByInventoryItemIdAsync(Guid inventoryItemId)
        {
            var logs = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
                .Where(l => l.InventoryItemId == inventoryItemId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return logs.Select(InventoryItemMapper.ToOrderDetailInventoryItemLogDto).ToList();
        }
    }


}
