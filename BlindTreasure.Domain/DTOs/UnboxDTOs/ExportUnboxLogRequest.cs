using System.ComponentModel;
using BlindTreasure.Domain.DTOs.Pagination;

namespace BlindTreasure.Domain.DTOs.UnboxDTOs;

public class ExportUnboxLogRequest : PaginationParameter
{
    public Guid? UserId { get; set; }
    public Guid? ProductId { get; set; }

    [DefaultValue("2025-08-16 04:20:47z")] public DateTime? FromDate { get; set; }
    [DefaultValue("2025-08-16 04:25:47z")] public DateTime? ToDate { get; set; }
}