namespace BlindTreasure.Domain.DTOs.CartItemDTOs;

public class CartDto
{
    public List<CartSellerItemDto> SellerItems { get; set; } = [];
    public int TotalQuantity => SellerItems.Sum(s => s.SellerTotalQuantity);
    public decimal TotalPrice => SellerItems.Sum(s => s.SellerTotalPrice);
}

public class CartSellerItemDto
{
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = [];
    public int SellerTotalQuantity => Items.Sum(i => i.Quantity);
    public decimal SellerTotalPrice => Items.Sum(i => i.TotalPrice);
    public Guid? PromotionId { get; set; } = null; // field này là tượng trưng nên chưa cần thiết
}