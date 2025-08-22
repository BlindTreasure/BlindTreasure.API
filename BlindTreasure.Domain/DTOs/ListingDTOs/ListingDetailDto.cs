using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ListingDTOs;

public class ListingDetailDto
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductImage { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsFree { get; set; }
    public string? Description { get; set; } // Mô tả listing
    public ListingStatus Status { get; set; }
    public DateTime ListedAt { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
}