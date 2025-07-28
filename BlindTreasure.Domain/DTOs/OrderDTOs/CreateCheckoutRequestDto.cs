namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class CreateCheckoutRequestDto
{
    public bool? IsShip { get; set; } = false; // có muốn ship hàng hay không
}