using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BlindTreasure.Application.Services;

public class OrderDetailInventoryItemLogShipmentService : IOrderDetailInventoryItemShipmentLogService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;

    public OrderDetailInventoryItemLogShipmentService(ICacheService cacheService, IClaimsService claimsService, ILoggerService logger, IUnitOfWork unitOfWork)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    // Implement methods for OrderDetailInventoryItemLogService here
    public async Task<OrderDetailInventoryItemShipmentLog> LogOrderDetailCreationAsync(OrderDetail orderDetail, string? msg)
    {
        var log = new OrderDetailInventoryItemShipmentLog
        {
            OrderDetailId = orderDetail.Id,
            ActionType = ActionType.ORDER_DETAIL_CREATED,
            LogContent = msg ?? $"Tạo chi tiết đơn hàng cho {(orderDetail.ProductId.HasValue ? "sản phẩm" : "blindbox")}",
            OldValue = null,
            NewValue = orderDetail.Status.ToString(),
            ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
            ValueStatusType = Domain.Entities.ValueType.ORDER_DETAIL
        };

        if (log.ActorId == null)
        {
            _logger.Warn("Thao tác này được thực hiện bởi hệ thống");
            log.LogContent += " (Thao tác hệ thống)";
        }

        var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
        return result;
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogShipmentAddedAsync(
        OrderDetail orderDetail,
        Shipment shipment, string? msg)
    {
        var log = new OrderDetailInventoryItemShipmentLog
        {
            OrderDetailId = orderDetail.Id,
            ActionType = ActionType.SHIPMENT_ADDED,
            LogContent = msg ?? $"Thêm vận chuyển: {shipment.Id} cho chi tiết đơn hàng này",
            OldValue = null,
            NewValue = shipment.Status.ToString(),
            ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
            ValueStatusType = Domain.Entities.ValueType.SHIPMENT
        };

        if (log.ActorId == null)
        {
            _logger.Warn("Thao tác này được thực hiện bởi hệ thống");
            log.LogContent += " (Thao tác hệ thống)";
        }

        var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
        return result;
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogShipmentOfOrderDetailChangedStatusAsync(
        OrderDetail orderDetail,
        ShipmentStatus oldStatus,
        Shipment shipmentNewStatus, string? msg)
    {
        var log = new OrderDetailInventoryItemShipmentLog
        {
            OrderDetailId = orderDetail.Id,
            ActionType = ActionType.SHIPMENT_ADDED,
            LogContent = msg ?? $"Vận chuyển thay đổi: {shipmentNewStatus.Id} cho chi tiết đơn hàng này, từ {oldStatus} sang {shipmentNewStatus.Status}",
            OldValue = oldStatus.ToString(),
            NewValue = shipmentNewStatus.Status.ToString(),
            ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
            ValueStatusType = Domain.Entities.ValueType.SHIPMENT
        };

        if (log.ActorId == null)
        {
            _logger.Warn("Thao tác này được thực hiện bởi hệ thống");
            log.LogContent += " (Thao tác hệ thống)";
        }

        var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
        return result;
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogOrderDetailStatusChangeAsync(
        OrderDetail orderDetail,
        OrderDetailItemStatus oldStatus,
        OrderDetailItemStatus newStatus, string? msg)
    {
        var log = new OrderDetailInventoryItemShipmentLog
        {
            OrderDetailId = orderDetail.Id,
            ActionType = ActionType.ORDER_DETAIL_STATUS_CHANGED,
            LogContent = msg ?? $"Cập nhật trạng thái chi tiết đơn hàng, chuyển sang: {newStatus}",
            OldValue = oldStatus.ToString(),
            NewValue = newStatus.ToString(),
            ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
            ValueStatusType = Domain.Entities.ValueType.ORDER_DETAIL
        };

        if (log.ActorId == null)
        {
            _logger.Warn("Thao tác này được thực hiện bởi hệ thống");
            log.LogContent += " (Thao tác hệ thống)";
        }

        var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
        return result;
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogInventoryItemOrCustomerBlindboxAddedAsync(
        OrderDetail orderDetail,
        InventoryItem? inventoryItem,
        CustomerBlindBox? blindBox, string? msg)
    {
        try
        {
            var orderDetaillog = new OrderDetailInventoryItemShipmentLog
            {
                OrderDetailId = orderDetail.Id,
                ActionType = inventoryItem != null ? ActionType.INVENTORY_ITEM_ADDED : ActionType.BLIND_BOX_ADDED,
                LogContent = msg ?? (inventoryItem != null
                    ? $"Tạo item kho: {inventoryItem.Id}"
                    : $"Thêm blindbox: {blindBox?.BlindBoxId}"),
                OldValue = null,
                NewValue = inventoryItem != null ? inventoryItem.Status.ToString() : "CHƯA MỞ",
                ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
                ValueStatusType = inventoryItem != null
                    ? Domain.Entities.ValueType.INVENTORY_ITEM
                    : Domain.Entities.ValueType.CUSTOM_BLINDBOX
            };

            var InventoryItemlog = new OrderDetailInventoryItemShipmentLog
            {
                OrderDetailId = orderDetail.Id,
                InventoryItemId = inventoryItem?.Id,
                ActionType = inventoryItem != null ? ActionType.INVENTORY_ITEM_ADDED : ActionType.BLIND_BOX_ADDED,
                LogContent = msg ?? (inventoryItem != null
                    ? $"Tạo item kho: {inventoryItem.Id}"
                    : $"Thêm blindbox: {blindBox?.BlindBoxId}"),
                OldValue = null,
                NewValue = inventoryItem != null ? inventoryItem.Status.ToString() : "CHƯA MỞ",
                ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
                ValueStatusType = inventoryItem != null
                    ? Domain.Entities.ValueType.INVENTORY_ITEM
                    : Domain.Entities.ValueType.CUSTOM_BLINDBOX
            };
            if (orderDetaillog.ActorId == null)
            {
                _logger.Warn("Thao tác này được thực hiện bởi hệ thống");
                orderDetaillog.LogContent += " (Thao tác hệ thống)";
                InventoryItemlog.LogContent += " (Thao tác hệ thống)";
            }

            var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(orderDetaillog);
            await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(InventoryItemlog);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi: " + ex.Message);
            throw;
        }
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogShipmentTrackingInventoryItemUpdateAsync(
        OrderDetail orderDetail,
        InventoryItemStatus oldStatus,
        InventoryItem InventoryItemWithNewStatus,
        string trackingMessage)
    {
        if (orderDetail == null)
        {
            _logger.Warn($"Chi tiết đơn hàng không liên kết với bất kỳ item kho nào. Không thể ghi log cập nhật tracking.");
            return null!;
        }

        var log = new OrderDetailInventoryItemShipmentLog
        {
            OrderDetailId = orderDetail.Id,
            InventoryItemId = InventoryItemWithNewStatus.Id,
            ActionType = ActionType.SHIPMENT_STATUS_CHANGED,
            LogContent = trackingMessage,
            OldValue = oldStatus.ToString(),
            NewValue = InventoryItemWithNewStatus.Status.ToString(),
            ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null,
            LogTime = DateTime.UtcNow,
            ValueStatusType = Domain.Entities.ValueType.INVENTORY_ITEM
        };

        if (log.ActorId == null)
        {
            _logger.Warn("Thao tác này được thực hiện bởi hệ thống");
            log.LogContent += " (Thao tác hệ thống)";
        }

        var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
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
            message.Append($"Vận chuyển {shipment.OrderCode}: Đã chuyển trạng thái từ {oldStatus} sang {newStatus}");

            switch (newStatus)
            {
                case ShipmentStatus.PROCESSING:
                    message.Append($" | Đã yêu cầu vận chuyển. Lịch lấy hàng dự kiến: {shipment.EstimatedPickupTime:yyyy-MM-dd HH:mm} tại {seller?.CompanyAddress}.");
                    break;

                case ShipmentStatus.PICKED_UP:
                    message.Append($" | Đã lấy hàng từ seller tại {seller?.CompanyAddress}. Ngày giao dự kiến: {shipment.EstimatedDelivery:yyyy-MM-dd}.");
                    break;

                case ShipmentStatus.IN_TRANSIT:
                    message.Append($" | Đang vận chuyển bởi GHN, ngày giao dự kiến: {shipment.EstimatedDelivery:yyyy-MM-dd}.");
                    break;

                case ShipmentStatus.DELIVERED:
                    message.Append($" | Đã giao cho khách tại {customerAddress?.AddressLine} vào lúc {DateTime.UtcNow:yyyy-MM-dd HH:mm}.");
                    break;

                default:
                    message.Append($" | Đã chuyển trạng thái từ {oldStatus} sang {newStatus}.");
                    break;
            }

            var logContent = message.ToString();

            // Log riêng cho entity Shipment
            await LogShipmentEntityStatusAsync(shipment, oldStatus, newStatus, logContent);

            var orderDetails = shipment.OrderDetails?.ToList();
            if (orderDetails == null)
            {
                shipment = await _unitOfWork.Shipments.GetQueryable()
                    .Include(s => s.OrderDetails)
                    .FirstOrDefaultAsync(s => s.Id == shipment.Id);
                orderDetails = shipment?.OrderDetails?.ToList();
            }

            if (orderDetails == null || !orderDetails.Any())
                foreach (var orderDetail in orderDetails)
                    await LogShipmentOfOrderDetailChangedStatusAsync(
                        orderDetail,
                        oldStatus,
                        shipment,
                        message.ToString()
                    );

            return message.ToString();
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi tạo message tracking: {ex.Message}");
            return $"Cập nhật vận chuyển: {oldStatus} → {newStatus}";
        }
    }

    private async Task LogShipmentEntityStatusAsync(
    Shipment shipment,
    ShipmentStatus oldStatus,
    ShipmentStatus newStatus,
    string logContent)
    {
        // Kiểm tra đã có log cho shipment với trạng thái này chưa
        var existingLog = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
            .Where(l => l.ShipmentId == shipment.Id && l.NewValue == newStatus.ToString() && l.OrderDetailId == null && l.InventoryItemId == null)
            .FirstOrDefaultAsync();

        if (existingLog != null)
            return;

        var log = new OrderDetailInventoryItemShipmentLog
        {
            ShipmentId = shipment.Id,
            OrderDetailId = null,
            InventoryItemId = null,
            LogContent = logContent,
            LogTime = DateTime.UtcNow,
            ActionType = ActionType.SHIPMENT_STATUS_CHANGED,
            ValueStatusType = Domain.Entities.ValueType.SHIPMENT,
            OldValue = oldStatus.ToString(),
            NewValue = newStatus.ToString(),
            ActorId = _claimsService.CurrentUserId != Guid.Empty ? _claimsService.CurrentUserId : null
        };

        await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
    }

    /// <summary>
    /// Lấy danh sách log theo OrderDetailId.
    /// </summary>
    public async Task<List<OrderDetailInventoryItemShipmentLogDto>> GetLogByOrderDetailIdAsync(Guid orderDetailId)
    {
        var logs = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
            .Where(l => l.OrderDetailId == orderDetailId && !l.InventoryItemId.HasValue)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return logs.Select(InventoryItemMapper.ToOrderDetailInventoryItemLogDto).ToList();
    }

    /// <summary>
    /// Lấy danh sách log theo InventoryItemId.
    /// </summary>
    public async Task<List<OrderDetailInventoryItemShipmentLogDto>> GetLogByInventoryItemIdAsync(Guid inventoryItemId)
    {
        var logs = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
            .Where(l => l.InventoryItemId == inventoryItemId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return logs.Select(InventoryItemMapper.ToOrderDetailInventoryItemLogDto).ToList();
    }

    public async Task<List<OrderDetailInventoryItemShipmentLogDto>> GetLogForShipmentByIdAsync(Guid shipmentId)
    {
        var logs = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
            .Where(l => l.ShipmentId == shipmentId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return logs.Select(InventoryItemMapper.ToOrderDetailInventoryItemLogDto).ToList();
    }
}