using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Mappers;

public static class UserMapper
{
    public static UserDto ToUserDto(User user)
    {
        return new UserDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            AvatarUrl = user.AvatarUrl,
            PhoneNumber = user.Phone,
            Status = user.Status,
            RoleName = user.RoleName,
            Gender = user.Gender,
            CreatedAt = user.CreatedAt
        };
    }
}