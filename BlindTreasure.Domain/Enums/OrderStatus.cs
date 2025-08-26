using System.ComponentModel;

namespace BlindTreasure.Domain.Enums;

public enum OrderStatus
{
    PENDING,
    CANCELLED,
    [Description("Đã thanh toán")] PAID,

    [Description("Đơn hàng được tạo hết hạn thanh toán")]
    EXPIRED,

    [Description("Đã hoàn thành, tất cả order-detail đã được giao thành công hoặc Item In-inventory hơn 3 ngày")]
    COMPLETED
}

public enum OrderDetailItemStatus
{
    [Description("Đã hoàn tiền")] REFUNDED,
    [Description("Chưa có yêu cầu ship")] PENDING,

    [Description("ĐANG Ở TRONG TÚI ĐỒ CỦA KHÁCH, CHƯA YÊU CẦU SHIP")]
    IN_INVENTORY,

    [Description("Đã request ship toàn bộ inventory item của order-detail")]
    SHIPPING_REQUESTED,

    [Description("Đã request ship một phần inventory item của order-detail")]
    PARTIALLY_SHIPPING_REQUESTED,

    [Description("Đang giao hết inventory item của order-detail")]
    DELIVERING,

    [Description("Đang giao một phần inventory item của order-detail")]
    PARTIALLY_DELIVERING,

    [Description("Khách đã nhận hết inventory item của order-detail")]
    DELIVERED,

    [Description("Đã nhận hết inventory item của order-detail")]
    PARTIALLY_DELIVERED,
    [Description("Đã hủy")] CANCELLED
}