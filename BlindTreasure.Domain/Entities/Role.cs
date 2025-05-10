using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Role : BaseEntity
{
    // ‘seller’, ‘customer’, ‘staff’, ‘admin’
    public RoleType Type { get; set; }

    // Mô tả chi tiết vai trò
    public string Description { get; set; }

    // 1-n → Users
    public ICollection<User> Users { get; set; }
}