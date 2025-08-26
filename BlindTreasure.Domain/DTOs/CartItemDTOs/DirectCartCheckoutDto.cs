using BlindTreasure.Domain.DTOs.OrderDTOs;

namespace BlindTreasure.Domain.DTOs.CartItemDTOs;

public class DirectCartCheckoutDto : CreateCheckoutRequestDto
{
    public List<CartSellerItemDto> SellerItems { get; set; } = new();
    public bool? IsShip { get; set; } = false; // có muốn ship hàng hay không
}