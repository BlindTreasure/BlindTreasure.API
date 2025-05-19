using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetUserDetailsByIdAsync(Guid userId);
    Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
    Task<UpdateAvatarResultDto?> UploadAvatarAsync(Guid userId, IFormFile file);

    //admin methods
    Task<UserDto?> CreateUserAsync(UserCreateDto dto);
    Task<UserDto?> DeleteUserAsync(Guid userId);
    Task<Pagination<UserDto>> GetAllUsersAsync(PaginationParameter param);
}