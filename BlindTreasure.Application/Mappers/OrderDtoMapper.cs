using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.PaymentDTOs;
using BlindTreasure.Domain.DTOs.TransactionDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Mappers;

public static class OrderDtoMapper
{
    public static OrderDto ToOrderDto(Order order)
    {
        try
        {
            return new OrderDto
            {
                Id = order.Id,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                FinalAmount = order.TotalAmount - (order.DiscountAmount ?? 0),
                PlacedAt = order.PlacedAt,
                CompletedAt = order.CompletedAt,
                ShippingAddress = order.ShippingAddress != null
                    ? ToOrderAddressDto(order.ShippingAddress)
                    : null,
                Details = order.OrderDetails?.Select(ToOrderDetailDto).ToList() ?? new List<OrderDetailDto>(),
                Payment = order.Payment != null ? ToPaymentDto(order.Payment) : null,
                PromotionId = order.PromotionId,
                DiscountAmount = order.DiscountAmount,
                PromotionNote = order.PromotionNote
                                ?? order.Promotion?.Description
                                ?? string.Empty
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
            ShippedAt = od.ShippedAt,
            ReceivedAt = od.ReceivedAt
        };
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
            TransactionId = payment.TransactionId,
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