using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UserDTOs;

public class UserCreateDto
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public RoleType RoleName { get; set; }
}