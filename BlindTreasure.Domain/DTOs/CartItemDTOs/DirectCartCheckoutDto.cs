using BlindTreasure.Domain.DTOs.OrderDTOs;

namespace BlindTreasure.Domain.DTOs.CartItemDTOs;

public class DirectCartCheckoutDto : CreateCheckoutRequestDto
{
    public List<CartSellerItemDto> SellerItems { get; set; } = new();
}

public class DirectCartItemDto
{
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid? BlindBoxId { get; set; }
    public string? BlindBoxName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }

    public Guid? PromotionId { get; set; } // voucher được apply theo seller nên phải tính trên item
    // Có thể bổ sung thêm payment info, note, v.v.
}