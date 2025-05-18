using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UserDTOs;

public class UserCreateDto
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public RoleType RoleName { get; set; }
}