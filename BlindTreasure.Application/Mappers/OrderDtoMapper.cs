using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.PaymentDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.DTOs.TransactionDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using Microsoft.IdentityModel.Tokens;

namespace BlindTreasure.Application.Mappers;

public static class OrderDtoMapper
{
    public static void UpdateOrderDetailStatusAndLogs(OrderDetail orderDetail)
    {
        var inventoryItems = orderDetail.InventoryItems?.ToList() ?? new List<InventoryItem>();
        if (!inventoryItems.Any())
            return;

        int total = inventoryItems.Count;
        int requested = inventoryItems.Count(ii => ii.Status == InventoryItemStatus.Shipment_requested);
        int delivering = inventoryItems.Count(ii => ii.Status == InventoryItemStatus.Delivering);

        var oldStatus = orderDetail.Status;

        // Trạng thái SHIPPING_REQUESTED
        if (requested == total)
            orderDetail.Status = OrderDetailItemStatus.SHIPPING_REQUESTED;
        else if (requested > 0)
            orderDetail.Status = OrderDetailItemStatus.PARTIALLY_SHIPPING_REQUESTED;

        // Trạng thái DELIVERING
        if (delivering == total)
            orderDetail.Status = OrderDetailItemStatus.DELIVERING;
        else if (delivering > 0)
            orderDetail.Status = OrderDetailItemStatus.PARTIALLY_DELIVERING;

        // Nếu chưa có inventory nào được request ship/delivering thì giữ nguyên (PENDING)

        // Ghi log thay đổi trạng thái nếu có
        if (orderDetail.Status != oldStatus)
        {
            orderDetail.Logs += $"\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Status changed: {oldStatus} → {orderDetail.Status}";
        }

        // Ghi log trạng thái inventory item hiện tại
        var logLines = inventoryItems
            .Select(ii => $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] InventoryItem {ii.Id}: {ii.Status}")
            .ToList();
        orderDetail.Logs += "\n" + string.Join("\n", logLines);
    }
    public static OrderDto ToOrderDto(Order order)
    {
        try
        {
            return new OrderDto
            {
                Id = order.Id,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                FinalAmount = order.TotalAmount,
                PlacedAt = order.PlacedAt,
                CompletedAt = order.CompletedAt,
                ShippingAddress = order.ShippingAddress != null
                    ? ToOrderAddressDto(order.ShippingAddress)
                    : null,
                Details = order.OrderDetails?.Select(ToOrderDetailDto).ToList() ?? new List<OrderDetailDto>(),
                Payment = order.Payment != null ? ToPaymentDto(order.Payment) : null,
                TotalShippingFee = order.TotalShippingFee ?? 0
            };
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public static OrderDetailDto ToOrderDetailDto(OrderDetail od)
    {
        return new OrderDetailDto
        {
            Id = od.Id,
            Logs = od.Logs,
            ProductId = od.ProductId,
            ProductName = od.Product?.Name,
            ProductImages = od.Product?.ImageUrls,
            BlindBoxId = od.BlindBoxId,
            BlindBoxName = od.BlindBox?.Name,
            BlindBoxImage = od.BlindBox?.ImageUrl,
            Quantity = od.Quantity,
            UnitPrice = od.UnitPrice,
            TotalPrice = od.TotalPrice,
            Status = od.Status,
           // Shipments = od.Shipments?.Select(ShipmentDtoMapper.ToShipmentDto).ToList() ?? new List<ShipmentDto>()
        };
    }

    public static OrderDetailDto ToOrderDetailDtoFullIncluded(OrderDetail od)
    {
        var result = ToOrderDetailDto(od);

        result.InventoryItems = od.InventoryItems?.Select(InventoryItemMapper.ToInventoryItemDto).ToList() ?? new List<InventoryItemDto>();
        if(result.Shipments.IsNullOrEmpty())
        result.Shipments = od.Shipments?.Select(ShipmentDtoMapper.ToShipmentDto).ToList() ?? new List<ShipmentDto>();

        return result;
    }



    public static OrderAddressDto ToOrderAddressDto(Address address)
    {
        return new OrderAddressDto
        {
            Id = address.Id,
            FullName = address.FullName,
            Phone = address.Phone,
            AddressLine = address.AddressLine,
            City = address.City,
            Province = address.Province,
            PostalCode = address.PostalCode,
            Country = address.Country
        };
    }

    public static PaymentDto ToPaymentDto(Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            DiscountRate = payment.DiscountRate,
            NetAmount = payment.NetAmount,
            Method = payment.Method,
            Status = payment.Status,
            PaymentIntentId = payment.PaymentIntentId,
            PaidAt = payment.PaidAt,
            RefundedAmount = payment.RefundedAmount,
            Transactions = payment.Transactions?.Select(ToTransactionDto).ToList() ?? new List<TransactionDto>()
        };
    }

    public static TransactionDto ToTransactionDto(Transaction t)
    {
        return new TransactionDto
        {
            Id = t.Id,
            Type = t.Type,
            Amount = t.Amount,
            Currency = t.Currency,
            Status = t.Status,
            OccurredAt = t.OccurredAt,
            ExternalRef = t.ExternalRef
        };
    }
}