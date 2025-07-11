namespace BlindTreasure.Domain.DTOs.ListingDTOs;

public class ListingDto
{
    public Guid Id { get; set; }
    public decimal Price { get; set; }
    public DateTime ListedAt { get; set; }
    public string Status { get; set; }

    public Guid InventoryId { get; set; }
    public string ProductName { get; set; }
    public string ProductImage { get; set; }
}