using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> CreateUserAsync(UserCreateDto dto);
    Task<bool> DeleteUserAsync(Guid userId);
    Task<Pagination<UserDto>> GetAllUsersAsync(PaginationParameter param);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<bool> UpdateUserAsync(Guid userId, UserUpdateDto dto);
    Task<UpdateAvatarResultDto?> UpdateUserAvatarAsync(Guid userId, IFormFile file);

    Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
    Task<UpdateAvatarResultDto?> UpdateAvatarAsync(Guid userId, IFormFile file);
}