namespace BlindTreasure.Domain.DTOs.CartItemDTOs;

public class AddCartItemDto
{
    public Guid? ProductId { get; set; }
    public Guid? BlindBoxId { get; set; }
    public int Quantity { get; set; }
}