using System.ComponentModel.DataAnnotations;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class User : BaseEntity
{
    [Required] [MaxLength(100)] public required string Email { get; set; }

    [MaxLength(255)] public string? Password { get; set; }

    [Required] [MaxLength(100)] public string? FullName { get; set; }

    [MaxLength(15)] public string? Phone { get; set; }

    public bool? Gender { get; set; }

    public required RoleType RoleName { get; set; }
    public Role? Role { get; set; }

    public UserStatus Status { get; set; }
    public bool IsEmailVerified { get; set; }

    // Tokens
    [MaxLength(128)] public string? RefreshToken { get; set; }
    [MaxLength(128)] public DateTime? RefreshTokenExpiryTime { get; set; }

    public DateTime DateOfBirth { get; set; }

    [MaxLength(512)] public string? AvatarUrl { get; set; }

    [MaxLength(512)] public string? Reason { get; set; }

    // Relationships
    public Seller Seller { get; set; }

    // Collections
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; }
    public ICollection<CustomerDiscount> CustomerDiscounts { get; set; }
    public ICollection<Order> Orders { get; set; }
    public ICollection<Address> Addresses { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<SupportTicket> SupportTickets { get; set; }
    public ICollection<Notification> Notifications { get; set; }
    public ICollection<Wishlist> Wishlists { get; set; }
    public ICollection<Certificate> VerifiedCertificates { get; set; }
    public ICollection<ProbabilityConfig> ApprovedProbabilityConfigs { get; set; }
}