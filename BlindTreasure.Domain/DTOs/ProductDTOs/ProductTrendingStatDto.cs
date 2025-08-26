namespace BlindTreasure.Domain.DTOs.ProductDTOs;

public class ProductTrendingStatDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public int TotalOrders { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
    public int ReviewCount { get; set; }
    public int FavouriteCount { get; set; }
    public double GrowthRate { get; set; } // % tăng trưởng 30 ngày gần nhất
}