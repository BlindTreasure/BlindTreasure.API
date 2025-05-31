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

    /// <summary>
    ///     Sắp xếp giảm dần (true) hay tăng dần (false).
    /// </summary>
    public bool Desc { get; set; } = true;
}