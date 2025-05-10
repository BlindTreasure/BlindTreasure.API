namespace BlindTreasure.Domain.Entities;

public class User : BaseEntity
{
    // Email đăng nhập
    public string Email { get; set; }

    // Mật khẩu đã mã hoá
    public string? Password { get; set; }

    // Họ và tên đầy đủ
    public string FullName { get; set; }

    // Số điện thoại liên hệ
    public string Phone { get; set; }

    // Trạng thái tài khoản (ACTIVE, LOCKED…)
    public string Status { get; set; }

    // FK → Role
    public Guid RoleId { get; set; }
    public Role Role { get; set; }

    public bool IsEmailVerified { get; set; }
    public string EmailVerifyToken { get; set; }
    public DateTime? EmailVerifyTokenExpires { get; set; }
    public string ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordExpires { get; set; }
    public string RefreshToken { get; set; }
    public string AvatarUrl { get; set; }
    public string PendingEmail { get; set; }
    public string PendingEmailVerifyToken { get; set; }
    public DateTime? PendingEmailVerifyExpires { get; set; }

    // 1-1 → Seller
    public Seller Seller { get; set; }

    // 1-n → CartItems, InventoryItems, CustomerDiscounts, Orders, Addresses, Reviews, SupportTickets, Notifications, Wishlists
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; }
    public ICollection<CustomerDiscount> CustomerDiscounts { get; set; }
    public ICollection<Order> Orders { get; set; }
    public ICollection<Address> Addresses { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<SupportTicket> SupportTickets { get; set; }
    public ICollection<Notification> Notifications { get; set; }
    public ICollection<Wishlist> Wishlists { get; set; }

    // 1-n cho các duyệt/phê duyệt của user
    public ICollection<Certificate> VerifiedCertificates { get; set; }
    public ICollection<ProbabilityConfig> ApprovedProbabilityConfigs { get; set; }
}