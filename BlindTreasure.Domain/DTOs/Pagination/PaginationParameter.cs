namespace BlindTreasure.Domain.DTOs.Pagination;

public class PaginationParameter
{
    private const int MaxPageSize = 50;

    private int _pageIndex = 1;
    private int _pageSize = 5;

    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    /// <summary>
    ///     Sắp xếp giảm dần (true) hay tăng dần (false).
    /// </summary>
    public bool Desc { get; set; } = true;
}