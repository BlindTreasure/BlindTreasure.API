namespace BlindTreasure.Domain.DTOs.CartItemDTOs;

public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public List<string>? ProductImages { get; set; }
    public Guid? BlindBoxId { get; set; }
    public string? BlindBoxName { get; set; }
    public string? BlindBoxImage { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AvailableStock { get; set; }    // số lượng còn lại trong kho

}