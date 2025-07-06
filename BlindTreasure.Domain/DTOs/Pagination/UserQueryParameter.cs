using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class UserQueryParameter : PaginationParameter
{
    /// <summary>
    ///     Tìm kiếm theo tên hoặc email.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    ///     Lọc theo trạng thái tài khoản.
    /// </summary>
    public UserStatus? Status { get; set; }

    /// <summary>
    ///     Lọc theo vai trò.
    /// </summary>
    public RoleType? RoleName { get; set; }

    /// <summary>
    ///     Trường dùng để sắp xếp. Mặc định: CreatedAt.
    /// </summary>
    public UserSortField SortBy { get; set; } = UserSortField.CreatedAt;

    // Có field Desc đã bỏ vào pagination
}