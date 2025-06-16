namespace BlindTreasure.Domain.DTOs.CartItemDTOs;

public class UpdateCartItemDto
{
    public Guid CartItemId { get; set; }
    public int Quantity { get; set; }
}