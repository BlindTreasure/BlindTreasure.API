using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

/// <summary>
/// Service for admin to manage users (CRUD, update avatar, etc.).
/// </summary>
public class UserService : IUserService
{
    private readonly ICacheService _cacheService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;

    public UserService(
        IUnitOfWork unitOfWork,
        ILoggerService logger,
        ICacheService cacheService,
        IBlobService blobService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _blobService = blobService;
    }

    // NÀY T LÀM TẠM TẠI ĐÓ GIỜ QUÊN LUÔN CÁCH PAGINATION R :)) TOÀN GRAPHQL VỚI ODATA
    public async Task<Pagination<UserDto>> GetAllUsersAsync(PaginationParameter param)
    {
        _logger.Info($"[GetAllUsersAsync] Admin requests user list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Users.GetQueryable().Where(u => !u.IsDeleted).AsNoTracking(); // này để tối ưu tốc độ vì chỉ cần read

        var count = await query.CountAsync(); // cái này bắt đầu truy xuống db bất đồng bộ nè
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();  // ko biết nên async chỗ này k nữa vì ở trên async rồi 

        var userDtos = users.Select(ToUserDto).ToList();
        var result = new Pagination<UserDto>(userDtos, count, param.PageIndex, param.PageSize);

        _logger.Info($"[GetAllUsersAsync] Returned {userDtos.Count} users.");
        return result;
    }

    /// <summary>
    /// Get user detail by id (with cache).
    /// </summary>
    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        _logger.Info($"[GetUserByIdAsync] Admin requests detail for user {userId}");
        var user = await GetUserById(userId, true);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[GetUserByIdAsync] User {userId} not found.");
            return null;
        }
        return ToUserDto(user);
    }

    /// <summary>
    /// Create a new user (admin can set any role).
    /// </summary>
    public async Task<UserDto?> CreateUserAsync(UserCreateDto dto)
    {
        try
        {
            _logger.Info($"[CreateUserAsync] Admin creates user {dto.Email}");

            if (await UserExistsAsync(dto.Email))
            {
                _logger.Warn($"[CreateUserAsync] Email {dto.Email} already exists.");
                return null;
            }

            var hashedPassword = new PasswordHasher().HashPassword(dto.Password);
            var user = new User
            {
                Email = dto.Email,
                Password = hashedPassword,
                FullName = dto.FullName,
                Phone = dto.PhoneNumber,
                DateOfBirth = dto.DateOfBirth,
                AvatarUrl = dto.AvatarUrl,
                Status = UserStatus.Active,
                RoleName = dto.RoleName,
                IsEmailVerified = true // Admin tạo thì mặc định đã xác thực
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
            await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

            _logger.Success($"[CreateUserAsync] User {user.Email} created by admin.");
            return ToUserDto(user);
        }
        catch (Exception ex)
        {
            _logger.Error($"[CreateUserAsync] failed: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Update user info (admin only, except avatar).
    /// </summary>
    public async Task<bool> UpdateUserAsync(Guid userId, UserUpdateDto dto)
    {
        try
        {
            _logger.Info($"[UpdateUserAsync] Admin updates user {userId}");

            var user = await GetUserById(userId, false);
            if (user == null || user.IsDeleted)
            {
                _logger.Warn($"[UpdateUserAsync] User {userId} not found.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName;
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                user.Phone = dto.PhoneNumber;
            if (dto.DateOfBirth.HasValue)
                user.DateOfBirth = dto.DateOfBirth.Value;
            if (dto.Gender.HasValue)
                user.Gender = dto.Gender.Value;
            if (dto.RoleName.HasValue)
                user.RoleName = dto.RoleName.Value;
            if (dto.Status.HasValue)
                user.Status = dto.Status.Value;

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
            await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

            _logger.Success($"[UpdateUserAsync] User {user.Email} updated by admin.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[UpdateUserAsync] failed: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Update user's avatar (admin only).
    /// </summary>
    public async Task<UpdateAvatarResultDto?> UpdateUserAvatarAsync(Guid userId, IFormFile file)
    {
        try
        {
            _logger.Info($"[UpdateUserAvatarAsync] Admin updates avatar for user {userId}");

            var user = await GetUserById(userId, false);
            if (user == null || user.IsDeleted)
            {
                _logger.Warn($"[UpdateUserAvatarAsync] User {userId} not found.");
                return null;
            }

            var fileName = $"user-avatars/{userId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            using (var stream = file.OpenReadStream())
            {
                await _blobService.UploadFileAsync(fileName, stream);
            }
            var fileUrl = await _blobService.GetFileUrlAsync(fileName);

            user.AvatarUrl = fileUrl;

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
            await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

            _logger.Success($"[UpdateUserAvatarAsync] Avatar updated for user {user.Email} by admin.");
            return new UpdateAvatarResultDto { AvatarUrl = user.AvatarUrl };
        }
        catch (Exception ex)
        {
            _logger.Error($"[UpdateUserAvatarAsync] failed: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Soft delete (deactivate) a user.
    /// </summary>
    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        try
        {
            _logger.Info($"[DeleteUserAsync] Admin deletes user {userId}");

            var user = await GetUserById(userId, false);
            if (user == null || user.IsDeleted)
            {
                _logger.Warn($"[DeleteUserAsync] User {userId} not found.");
                return false;
            }

            await _unitOfWork.Users.SoftRemove(user);
            user.Status = UserStatus.Locked;
            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.RemoveAsync($"user:{user.Email}");
            await _cacheService.RemoveAsync($"user:{user.Id}");

            _logger.Success($"[DeleteUserAsync] User {user.Email} deactivated by admin.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[DeleteUserAsync] failed: {ex}");
            return false;
        }
    }

    // ----------------- PRIVATE HELPER METHODS -----------------

    /// <summary>
    /// Checks if a user exists in cache or DB.
    /// </summary>
    private async Task<bool> UserExistsAsync(string email)
    {
        var cacheKey = $"user:{email}";
        var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
        if (cachedUser != null) return true;

        var existingUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        return existingUser != null;
    }

    /// <summary>
    /// Gets a user by id, optionally using cache.
    /// </summary>
    private async Task<User?> GetUserById(Guid id, bool useCache = false)
    {
        if (useCache)
        {
            var cacheKey = $"user:{id}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
            if (cachedUser != null) return cachedUser;

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user != null)
                await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
            return user;
        }

        return await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Maps User entity to UserDto.
    /// </summary>
    private static UserDto ToUserDto(User user)
    {
        return new UserDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            AvatarUrl = user.AvatarUrl,
            PhoneNumber = user.Phone,
            RoleName = user.RoleName,
            Gender = user.Gender,
            CreatedAt = user.CreatedAt
        };
    }
}