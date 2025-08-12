using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Entities
{
    public class OrderDetailInventoryItemLog : BaseEntity
    {
        public Guid OrderDetailId { get; set; }
        public OrderDetail OrderDetail { get; set; }

        public Guid InventoryItemId { get; set; }
        public InventoryItem InventoryItem { get; set; }
        [MaxLength(1000)]
        public string? LogContent { get; set; } = string.Empty; // Lưu trữ nhật ký trạng thái
        public DateTime? LogTime { get; set; } = DateTime.UtcNow; // Lưu trữ thời gian nhật ký, định dạng ISO 8601

        public LogType? LogType { get; set; }

    }

    public enum LogType
    {
        SHIPMENT,
        INVENTORY_ITEM,
        PAYMENT
    }

}