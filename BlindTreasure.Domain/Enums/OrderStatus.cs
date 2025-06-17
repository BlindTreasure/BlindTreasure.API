namespace BlindTreasure.Domain.Enums;

public enum OrderStatus
{
    PENDING,
    CANCELLED,
    PAID,
    FAILED,
    COMPLETED,
    EXPIRED
}

public enum OrderDetailStatus
{
    PENDING,
    SHIPPED,
    DELIVERED,
    CANCELLED
}