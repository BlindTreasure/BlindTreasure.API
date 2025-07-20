namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class CreateCheckoutRequestDto
{
    public bool? IsShip { get; set; } = false; // có muốn ship hàng hay không
    public Guid? PromotionId { get; set; } // voucher được apply theo seller nên phải tính trên order
}