using BlindTreasure.Domain.DTOs.Pagination;

namespace BlindTreasure.Domain.DTOs.UnboxDTOs;

public class UnboxLogsQueryParameter : PaginationParameter
{
    public Guid? UserId { get; set; }
    public Guid? ProductId { get; set; }
}