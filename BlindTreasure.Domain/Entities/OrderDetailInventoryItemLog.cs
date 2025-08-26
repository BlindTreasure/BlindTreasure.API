using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.Entities;

public class OrderDetailInventoryItemLog : BaseEntity
{
    public Guid? OrderDetailId { get; set; }
    public OrderDetail? OrderDetail { get; set; }

    public Guid? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    [MaxLength(1000)] public string? LogContent { get; set; } = string.Empty; // Lưu trữ nhật ký trạng thái
    public DateTime? LogTime { get; set; } = DateTime.UtcNow; // Lưu trữ thời gian nhật ký, định dạng ISO 8601

    public ActionType? ActionType { get; set; }
    public ValueType? ValueStatusType { get; set; }
    public string? OldValue { get; set; } // Trạng thái trước thay đổi
    public string? NewValue { get; set; } // Trạng thái mới
    public Guid? ActorId { get; set; } // User/System thực hiện hành động
}

public enum ActionType
{
    ORDER_DETAIL_CREATED,
    SHIPMENT_ADDED,
    INVENTORY_ITEM_ADDED,
    SHIPMENT_STATUS_CHANGED,
    ORDER_DETAIL_STATUS_CHANGED,
    BLIND_BOX_ADDED,
    PAYMENT_STATUS_CHANGED
}

public enum ValueType
{
    ORDER_DETAIL,
    SHIPMENT,
    INVENTORY_ITEM,
    CUSTOM_BLINDBOX,
    PAYMENT
}