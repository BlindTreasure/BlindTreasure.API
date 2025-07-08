using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class InventoryItemQueryParameter : PaginationParameter
{
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public InventoryItemStatus? Status { get; set; } // enum
    public bool? IsFromBlindBox { get; set; }
}