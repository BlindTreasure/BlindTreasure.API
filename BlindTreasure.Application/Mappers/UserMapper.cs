using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Mappers;

public static class UserMapper
{
    public static UserDto ToUserDto(User user)
    {
        if (user.Seller != null)
            return new UserDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                DateOfBirth = user.DateOfBirth,
                AvatarUrl = user.AvatarUrl,
                Status = user.Status,
                PhoneNumber = user.Phone,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt,
                SellerId = user.Seller.Id
            };


        return new UserDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            PhoneNumber = user.Phone,
            RoleName = user.RoleName,
            CreatedAt = user.CreatedAt,
        };
    }
}