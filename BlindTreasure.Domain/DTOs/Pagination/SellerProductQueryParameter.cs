namespace BlindTreasure.Domain.DTOs.Pagination;

public class SellerProductQueryParameter
{
    /// <summary>
    ///     Tìm kiếm theo tên sản phẩm.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    ///     Lọc theo danh mục.
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    ///     Lọc theo trạng thái sản phẩm.
    /// </summary>
    public string? Status { get; set; }


    /// <summary>
    ///     Sắp xếp theo trường nào. Mặc định: CreatedAt.
    /// </summary>
    public ProductSortField SortBy { get; set; } = ProductSortField.CreatedAt;

    // Có field Desc đã bỏ vào pagination

}