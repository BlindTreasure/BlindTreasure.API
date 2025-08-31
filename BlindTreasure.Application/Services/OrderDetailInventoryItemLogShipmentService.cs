using System.Text;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValueType = BlindTreasure.Domain.Entities.ValueType;

namespace BlindTreasure.Application.Services;

public class OrderDetailInventoryItemLogShipmentService : IOrderDetailInventoryItemShipmentLogService
{
    // Constants for system messages
    private const string SYSTEM_OPERATION_SUFFIX = " (Thao tác hệ thống)";
    private const string SYSTEM_OPERATION_WARNING = "Thao tác này được thực hiện bởi hệ thống";
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;

    public OrderDetailInventoryItemLogShipmentService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService logger,
        IUnitOfWork unitOfWork)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogOrderDetailCreationAsync(OrderDetail orderDetail,
        string? msg)
    {
        var itemType = orderDetail.ProductId.HasValue ? "sản phẩm" : "blindbox";
        var logContent = msg ?? $"Tạo chi tiết đơn hàng cho {itemType}";

        var log = CreateBaseLog(
            orderDetail.Id,
            ActionType.ORDER_DETAIL_CREATED,
            logContent,
            null,
            orderDetail.Status.ToString(),
            ValueType.ORDER_DETAIL
        );

        return await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogShipmentAddedAsync(
        OrderDetail orderDetail,
        Shipment shipment,
        string? msg)
    {
        var logContent = msg ?? $"Thêm vận chuyển: {shipment.Id} cho chi tiết đơn hàng này";

        var log = CreateBaseLog(
            orderDetail.Id,
            ActionType.SHIPMENT_ADDED,
            logContent,
            null,
            shipment.Status.ToString(),
            ValueType.SHIPMENT
        );

        return await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogShipmentOfOrderDetailChangedStatusAsync(
        OrderDetail orderDetail,
        ShipmentStatus oldStatus,
        Shipment shipmentNewStatus,
        string? msg)
    {
        var logContent = msg ??
                         $"Vận chuyển thay đổi: {shipmentNewStatus.Id} cho chi tiết đơn hàng này, từ {oldStatus} sang {shipmentNewStatus.Status}";

        var log = CreateBaseLog(
            orderDetail.Id,
            ActionType.SHIPMENT_ADDED,
            logContent,
            oldStatus.ToString(),
            shipmentNewStatus.Status.ToString(),
            ValueType.SHIPMENT
        );

        return await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogOrderDetailStatusChangeAsync(
        OrderDetail orderDetail,
        OrderDetailItemStatus oldStatus,
        OrderDetailItemStatus newStatus,
        string? msg)
    {
        var logContent = msg ?? $"Cập nhật trạng thái chi tiết đơn hàng, chuyển sang: {newStatus}";

        var log = CreateBaseLog(
            orderDetail.Id,
            ActionType.ORDER_DETAIL_STATUS_CHANGED,
            logContent,
            oldStatus.ToString(),
            newStatus.ToString(),
            ValueType.ORDER_DETAIL
        );

        return await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogInventoryItemOrCustomerBlindboxAddedAsync(
        OrderDetail orderDetail,
        InventoryItem? inventoryItem,
        CustomerBlindBox? blindBox,
        string? msg)
    {
        try
        {
            var isInventoryItem = inventoryItem != null;
            var actionType = isInventoryItem ? ActionType.INVENTORY_ITEM_ADDED : ActionType.BLIND_BOX_ADDED;
            var valueType = isInventoryItem
                ? ValueType.INVENTORY_ITEM
                : ValueType.CUSTOM_BLINDBOX;

            var logContent = msg ?? (isInventoryItem
                ? $"Tạo item kho: {inventoryItem.Id}"
                : $"Thêm blindbox: {blindBox?.BlindBoxId}");

            var newValue = isInventoryItem ? inventoryItem.Status.ToString() : "CHƯA MỞ";

            // Create order detail log
            var orderDetailLog = CreateBaseLog(
                orderDetail.Id,
                actionType,
                logContent,
                null,
                newValue,
                valueType
            );

            // Create inventory item log with InventoryItemId
            var inventoryItemLog = CreateBaseLog(
                orderDetail.Id,
                actionType,
                logContent,
                null,
                newValue,
                valueType,
                inventoryItem?.Id
            );

            var result = await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(orderDetailLog);
            await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(inventoryItemLog);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi: {ex.Message}");
            throw;
        }
    }

    public async Task<OrderDetailInventoryItemShipmentLog> LogShipmentTrackingInventoryItemUpdateAsync(
        OrderDetail orderDetail,
        InventoryItemStatus oldStatus,
        InventoryItem inventoryItemWithNewStatus,
        string trackingMessage)
    {
        if (orderDetail == null)
        {
            _logger.Warn(
                "Chi tiết đơn hàng không liên kết với bất kỳ item kho nào. Không thể ghi log cập nhật tracking.");
            return null!;
        }

        var log = CreateBaseLog(
            orderDetail.Id,
            ActionType.SHIPMENT_STATUS_CHANGED,
            trackingMessage,
            oldStatus.ToString(),
            inventoryItemWithNewStatus.Status.ToString(),
            ValueType.INVENTORY_ITEM,
            inventoryItemWithNewStatus.Id
        );

        log.LogTime = DateTime.UtcNow;

        return await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
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
            var message = BuildTrackingMessage(shipment, oldStatus, newStatus, seller, customerAddress);
            var logContent = message.ToString();

            // Log for shipment entity
            await LogShipmentEntityStatusAsync(shipment, oldStatus, newStatus, logContent);

            // Get order details efficiently
            var orderDetails = await GetShipmentOrderDetailsAsync(shipment);

            // Log for each order detail
            if (orderDetails?.Any() == true)
            {
                var logTasks = orderDetails.Select(orderDetail =>
                    LogShipmentOfOrderDetailChangedStatusAsync(orderDetail, oldStatus, shipment, logContent));

                await Task.WhenAll(logTasks);
            }

            return logContent;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi tạo message tracking: {ex.Message}");
            return $"Cập nhật vận chuyển: {oldStatus} → {newStatus}";
        }
    }

    public async Task<List<OrderDetailInventoryItemShipmentLogDto>> GetLogByOrderDetailIdAsync(Guid orderDetailId)
    {
        var logs = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
            .Where(l => l.OrderDetailId == orderDetailId && !l.InventoryItemId.HasValue)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return logs.Select(InventoryItemMapper.ToOrderDetailInventoryItemLogDto).ToList();
    }

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

    #region Private Helper Methods

    private OrderDetailInventoryItemShipmentLog CreateBaseLog(
        Guid orderDetailId,
        ActionType actionType,
        string logContent,
        string? oldValue,
        string newValue,
        ValueType valueStatusType,
        Guid? inventoryItemId = null)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var actorId = currentUserId != Guid.Empty ? currentUserId : Guid.Empty;

        var finalLogContent = logContent;
        if (actorId == null)
        {
            _logger.Warn(SYSTEM_OPERATION_WARNING);
            finalLogContent += SYSTEM_OPERATION_SUFFIX;
        }

        return new OrderDetailInventoryItemShipmentLog
        {
            OrderDetailId = orderDetailId,
            InventoryItemId = inventoryItemId,
            ActionType = actionType,
            LogContent = finalLogContent,
            OldValue = oldValue,
            NewValue = newValue,
            ActorId = currentUserId != Guid.Empty ? currentUserId : Guid.Empty,
            ValueStatusType = valueStatusType
        };
    }

    private StringBuilder BuildTrackingMessage(
        Shipment shipment,
        ShipmentStatus oldStatus,
        ShipmentStatus newStatus,
        Seller seller,
        Address customerAddress)
    {
        var message = new StringBuilder();
        message.Append($"Vận chuyển {shipment.OrderCode}: Đã chuyển trạng thái từ {oldStatus} sang {newStatus}");

        switch (newStatus)
        {
            case ShipmentStatus.PROCESSING:
                message.Append(
                    $" | Đã yêu cầu vận chuyển. Lịch lấy hàng dự kiến: {shipment.EstimatedPickupTime:yyyy-MM-dd HH:mm} tại {seller?.CompanyAddress}.");
                break;

            case ShipmentStatus.PICKED_UP:
                message.Append(
                    $" | Đã lấy hàng từ seller tại {seller?.CompanyAddress}. Ngày giao dự kiến: {shipment.EstimatedDelivery:yyyy-MM-dd}.");
                break;

            case ShipmentStatus.IN_TRANSIT:
                message.Append(
                    $" | Đang vận chuyển bởi GHN, ngày giao dự kiến: {shipment.EstimatedDelivery:yyyy-MM-dd}.");
                break;

            case ShipmentStatus.DELIVERED:
                message.Append(
                    $" | Đã giao cho khách tại {customerAddress?.AddressLine} vào lúc {DateTime.UtcNow:yyyy-MM-dd HH:mm}.");
                break;

            default:
                message.Append($" | Đã chuyển trạng thái từ {oldStatus} sang {newStatus}.");
                break;
        }

        return message;
    }

    private async Task<List<OrderDetail>?> GetShipmentOrderDetailsAsync(Shipment shipment)
    {
        var orderDetails = shipment.OrderDetails?.ToList();

        if (orderDetails == null || !orderDetails.Any())
        {
            var shipmentWithDetails = await _unitOfWork.Shipments.GetQueryable()
                .Include(s => s.OrderDetails)
                .FirstOrDefaultAsync(s => s.Id == shipment.Id);

            orderDetails = shipmentWithDetails?.OrderDetails?.ToList();
        }

        return orderDetails;
    }

    private async Task LogShipmentEntityStatusAsync(
        Shipment shipment,
        ShipmentStatus oldStatus,
        ShipmentStatus newStatus,
        string logContent)
    {
        // Check if log already exists for this shipment status
        var existingLog = await _unitOfWork.OrderDetailInventoryItemLogs.GetQueryable()
            .Where(l => l.ShipmentId == shipment.Id &&
                        l.NewValue == newStatus.ToString() &&
                        l.OrderDetailId == null &&
                        l.InventoryItemId == null)
            .FirstOrDefaultAsync();

        if (existingLog != null)
            return;

        var currentUserId = _claimsService.CurrentUserId;
        var actorId = currentUserId != Guid.Empty ? currentUserId : Guid.Empty;

        var log = new OrderDetailInventoryItemShipmentLog
        {
            ShipmentId = shipment.Id,
            OrderDetailId = null,
            InventoryItemId = null,
            LogContent = logContent,
            LogTime = DateTime.UtcNow,
            ActionType = ActionType.SHIPMENT_STATUS_CHANGED,
            ValueStatusType = ValueType.SHIPMENT,
            OldValue = oldStatus.ToString(),
            NewValue = newStatus.ToString(),
            ActorId = currentUserId != Guid.Empty ? currentUserId : Guid.Empty
        };

        await _unitOfWork.OrderDetailInventoryItemLogs.AddAsync(log);
    }

    #endregion
}