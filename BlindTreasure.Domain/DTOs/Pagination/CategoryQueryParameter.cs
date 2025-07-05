namespace BlindTreasure.Domain.DTOs.Pagination;

public class CategoryQueryParameter : PaginationParameter
{
    /// <summary>
    ///     Tìm kiếm theo tên danh mục. (sử dụng param: SearchName)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    ///     Sắp xếp theo trường nào. Mặc định: CreatedAt.
    /// </summary>
    public CategorySortField SortBy { get; set; } = CategorySortField.CreatedAt;

    
}

public enum CategorySortField
{
    CreatedAt,
    Name
}