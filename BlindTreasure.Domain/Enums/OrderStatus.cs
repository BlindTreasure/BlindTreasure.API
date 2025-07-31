namespace BlindTreasure.Domain.Enums;

public enum OrderStatus
{
    PENDING,
    CANCELLED,
    PAID,
    EXPIRED
}

public enum OrderDetailItemStatus
{
    PENDING, // Chưa có yêu cầu ship
    SHIPPING_REQUESTED, // Đã request ship toàn bộ inventory item của order-detail
    PARTIALLY_SHIPPING_REQUESTED, // Đã request ship một phần inventory item của order-detail
    DELIVERING, // Đang giao hết inventory item của order-detail
    PARTIALLY_DELIVERING, // Đang giao một phần inventory item của order-detail
    DELIVERED, // Khách đã nhận hết inventory item của order-detail
    PARTIALLY_DELIVERED, // Đã nhận hết inventory item của order-detail
    CANCELLED // Đã hủy,
}