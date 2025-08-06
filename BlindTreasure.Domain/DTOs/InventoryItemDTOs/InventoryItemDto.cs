using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public ProducDetailDto? Product { get; set; }
    public string ProductName { get; set; }
    public string Image { get; set; }
    public string Location { get; set; }
    public InventoryItemStatus Status { get; set; } // enum
    public DateTime CreatedAt { get; set; }

    public bool IsFromBlindBox { get; set; }
    public Guid? SourceCustomerBlindBoxId { get; set; }
    public HoldInfoDto? HoldInfo { get; set; }
    public Guid? OrderDetailId { get; set; }
    public OrderDetailDto? OrderDetail { get; set; }
    public Guid? ShipmentId { get; set; }
    public ShipmentDto? Shipment { get; set; }

    public bool IsOnHold { get; set; }
    public bool HasActiveListing { get; set; }
}