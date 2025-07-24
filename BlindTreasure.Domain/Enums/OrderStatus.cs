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
    PENDING,     // Chưa có yêu cầu ship
    SHIPPING_REQUESTED,   // Đã request ship (một phần hoặc toàn bộ)
    DELIVERING,  // Đang giao (một phần hoặc toàn bộ)
    DELIVERED,   // Đã nhận hết
    CANCELLED    // Đã hủy,
}