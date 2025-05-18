using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UserDTOs;

public class UserUpdateDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public RoleType? RoleName { get; set; }
    public UserStatus? Status { get; set; }
    public bool? Gender { get; set; }
}