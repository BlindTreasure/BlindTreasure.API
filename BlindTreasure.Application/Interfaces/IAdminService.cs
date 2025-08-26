using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface IAdminService
{
    Task<UserDto?> GetUserDetailsByIdAsync(Guid userId);
    Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
    Task<UpdateAvatarResultDto?> UploadAvatarAsync(Guid userId, IFormFile file);

    //admin methods
    Task<UserDto?> CreateUserAsync(UserCreateDto dto);
    Task<UserDto?> UpdateUserStatusAsync(Guid userId, UserStatus newStatus, string? reason = null);
    Task<Pagination<UserDto>> GetAllUsersAsync(UserQueryParameter param);
    Task<User?> GetUserByEmail(string email, bool useCache = false);
    Task<User?> GetUserById(Guid id, bool useCache = false);
    Task<bool> TryCompleteOrderAsync(Order order, CancellationToken cancellationToken = default);

    //AI analysis
}