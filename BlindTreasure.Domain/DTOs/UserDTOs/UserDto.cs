using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UserDTOs;

public class UserDto
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public string? PhoneNumber { get; set; }
    public RoleType? RoleName { get; set; }
    public DateTime CreatedAt { get; set; }
}