using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Pagination;

/// <summary>
///     Tham số truy vấn phân trang và lọc user cho admin.
/// </summary>
public class UserQueryParameter : PaginationParameter
{
    /// <summary>
    ///     Tìm kiếm theo tên hoặc email (chứa chuỗi).
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    ///     Lọc chính xác theo email.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     Lọc chính xác theo tên đầy đủ.
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    ///     Lọc theo trạng thái tài khoản (Active, Locked, Suspended, Pending).
    /// </summary>
    public UserStatus? Status { get; set; }

    /// <summary>
    ///     Lọc theo vai trò (Admin, Staff, Seller, Customer).
    /// </summary>
    public RoleType? RoleName { get; set; }

    /// <summary>
    ///     Sắp xếp theo trường nào (createdAt, email, fullName...).
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    ///     Sắp xếp giảm dần (true) hay tăng dần (false). Mặc định: true.
    /// </summary>
    public bool Desc { get; set; } = true;
}