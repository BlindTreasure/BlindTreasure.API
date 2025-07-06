namespace BlindTreasure.Domain.DTOs.Pagination;

public class CustomerBlindBoxQueryParameter : PaginationParameter
{
    public bool? IsOpened { get; set; }
    public string? Search { get; set; } // Lọc theo tên BlindBox nếu muốn
    public Guid? BlindBoxId { get; set; }
}