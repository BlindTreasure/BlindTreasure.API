namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class CreateInventoryItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
}