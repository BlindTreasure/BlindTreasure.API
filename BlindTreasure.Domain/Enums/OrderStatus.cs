namespace BlindTreasure.Domain.Enums;

public enum OrderStatus
{
    PENDING,
    CANCELLED,
    PAID,
    COMPLETED,
    EXPIRED
}

public enum OrderDetailStatus
{
    PENDING,
    SHIPPING_REQUESTED,
    DELIVERING,
    DELIVERED,
    CANCELLED
}