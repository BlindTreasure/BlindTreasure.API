namespace BlindTreasure.Domain.DTOs.CartItemDTOs;

public class CartDto
{
    public List<CartItemDto> Items { get; set; } = [];
    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public decimal TotalPrice => Items.Sum(i => i.TotalPrice);
}