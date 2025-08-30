using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValueType = BlindTreasure.Domain.Entities.ValueType;

namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class OrderDetailInventoryItemShipmentLogDto
{
    public Guid Id { get; set; }
    public Guid? OrderDetailId { get; set; }
    public Guid? ShipmentId { get; set; }
    public Guid? InventoryItemId { get; set; }
    [MaxLength(1000)] public string? LogContent { get; set; } = string.Empty; // Lưu trữ nhật ký trạng thái
    public DateTime? LogTime { get; set; } = DateTime.UtcNow; // Lưu trữ thời gian nhật ký, định dạng ISO 8601

    public ActionType? ActionType { get; set; }
    public string? OldValue { get; set; } // Trạng thái trước thay đổi
    public string? NewValue { get; set; } // Trạng thái mới
    public Guid? ActorId { get; set; }
    public ValueType? ValueStatusType { get; set; }
}