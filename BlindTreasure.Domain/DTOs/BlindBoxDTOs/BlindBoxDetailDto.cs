using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class BlindBoxDetailDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public BlindBoxStockStatus StockStatus { get; set; }
    public string? CategoryName { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime ReleaseDate { get; set; }
    public BlindBoxStatus Status { get; set; } //Draft, PendingApproval, Approved...
    public bool HasSecretItem { get; set; }
    public int SecretProbability { get; set; }
    public string? RejectReason { get; set; }

    public bool IsDeleted { get; set; }
    public List<BlindBoxItemDto>? Items { get; set; }
}