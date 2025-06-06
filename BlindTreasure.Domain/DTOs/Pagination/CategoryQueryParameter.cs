namespace BlindTreasure.Domain.DTOs.Pagination;

public class CategoryQueryParameter : PaginationParameter
{
    /// <summary>
    /// Tìm kiếm theo tên danh mục. (sử dụng param: SearchName)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    ///     Sắp xếp theo trường nào. Mặc định: CreatedAt.
    /// </summary>
    public CategorySortField SortBy { get; set; } = CategorySortField.CreatedAt;

    /// <summary>
    ///     Sắp xếp giảm dần (true) hay tăng dần (false).
    /// </summary>
    public bool Desc { get; set; } = true;
}

public enum CategorySortField
{
    CreatedAt,
    Name
}