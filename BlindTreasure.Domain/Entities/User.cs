namespace BlindTreasure.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string Status { get; set; }

    public Guid RoleId { get; set; }
    public Role Role { get; set; }

    public bool IsEmailVerified { get; set; }
    public string EmailVerifyToken { get; set; }
    public DateTime EmailVerifyTokenExpires { get; set; }
    public string ResetPasswordToken { get; set; }
    public DateTime ResetPasswordExpires { get; set; }
    public string RefreshToken { get; set; }
    public string AvatarUrl { get; set; }

    public string PendingEmail { get; set; }
    public string PendingEmailVerifyToken { get; set; }
    public DateTime PendingEmailVerifyExpires { get; set; }

    // 1-1: User → Seller (nếu có)
    public Seller Seller { get; set; }

    // 1-n khác
    public ICollection<VerificationRequest> VerificationRequests { get; set; }
    public ICollection<Cart> Carts { get; set; }
    public ICollection<Order> Orders { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<SupportTicket> SupportTickets { get; set; }
    public ICollection<Notification> Notifications { get; set; }
    public ICollection<Address> Addresses { get; set; }
    public ICollection<Wishlist> Wishlists { get; set; }
}