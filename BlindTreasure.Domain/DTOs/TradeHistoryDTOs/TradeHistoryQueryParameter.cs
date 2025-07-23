using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.TradeHistoryDTOs;

public class TradeHistoryQueryParameter : PaginationParameter
{
    // Filter options
    public TradeRequestStatus? FinalStatus { get; set; }
    public Guid? RequesterId { get; set; }
    public Guid? ListingId { get; set; }
    public DateTime? CompletedFromDate { get; set; }
    public DateTime? CompletedToDate { get; set; }
    public DateTime? CreatedFromDate { get; set; }
    public DateTime? CreatedToDate { get; set; }

    // Sort field - default là CompletedAt
    public string SortBy { get; set; } = "CompletedAt";
}