using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class OrderDetailDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? OrderId { get; set; }

    public string? ProductName { get; set; }
    public List<string>? ProductImages { get; set; }
    public Guid? BlindBoxId { get; set; }
    public string? BlindBoxName { get; set; }
    public string? BlindBoxImage { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public OrderDetailStatus Status { get; set; }
    public ICollection<ShipmentDto> Shipments { get; set; } = new List<ShipmentDto>();
}