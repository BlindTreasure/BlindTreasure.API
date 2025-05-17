using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class UserService : IUserService
{
    private readonly ICacheService _cacheService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork, ILoggerService loggerService, ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _cacheService = cacheService;
    }

    public async Task<UserDto> GetUserDetails(Guid id)
    {
        _loggerService.Info($"[GetUserDetails] Start fetching user with ID: {id}");

        if (id == Guid.Empty)
            throw new Exception("400|ID người dùng không hợp lệ.");

        var cacheKey = $"user:{id}";
        var user = await _cacheService.GetAsync<User>(cacheKey);

        if (user == null)
        {
            user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                _loggerService.Warn($"[GetUserDetails] Không tìm thấy user với ID: {id}");
                throw new Exception("404|Không tìm thấy người dùng.");
            }

            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
        }

        _loggerService.Info($"[GetUserDetails] Đã lấy thông tin user ID: {id}");

        return ToUserDto(user);
    }

    /// <summary>
    /// Get user detail by id.
    /// </summary>
    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        _loggerService.Info($"[GetUserByIdAsync] Admin requests detail for user {userId}");
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _loggerService.Warn($"[GetUserByIdAsync] User {userId} not found.");
            return null;
        }
        return ToUserDto(user);
    }

    public async Task<UserDto?> CreateUserAsync(UserCreateDto dto)
    {
        try
        {
            _loggerService.Info($"[CreateUserAsync] Admin creates user {dto.Email}");

            if (await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == dto.Email) != null)
            {
                _loggerService.Warn($"[CreateUserAsync] Email {dto.Email} already exists.");
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

            _loggerService.Success($"[CreateUserAsync] User {user.Email} created by admin.");
            return ToUserDto(user);
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[CreateUserAsync] failed: {ex}");
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UserUpdateDto dto)
    {
        try
        {
            _loggerService.Info($"[UpdateUserAsync] Admin updates user {userId}");

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
            {
                _loggerService.Warn($"[UpdateUserAsync] User {userId} not found.");
                return false;
            }

            user.FullName = dto.FullName ?? user.FullName;
            user.Phone = dto.PhoneNumber ?? user.Phone;
            user.DateOfBirth = dto.DateOfBirth ?? user.DateOfBirth;
            user.AvatarUrl = dto.AvatarUrl ?? user.AvatarUrl;
            user.RoleName = dto.RoleName ?? user.RoleName;
            user.Status = dto.Status ?? user.Status;

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));

            _loggerService.Success($"[UpdateUserAsync] User {user.Email} updated by admin.");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[UpdateUserAsync] failed: {ex}");
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        try
        {
            _loggerService.Info($"[DeleteUserAsync] Admin deletes user {userId}");

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
            {
                _loggerService.Warn($"[DeleteUserAsync] User {userId} not found.");
                return false;
            }

            await _unitOfWork.Users.SoftRemove(user); // isdelete = true
            user.Status = UserStatus.Locked;
            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.RemoveAsync($"user:{user.Email}");

            _loggerService.Success($"[DeleteUserAsync] User {user.Email} deactivated by admin.");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[DeleteUserAsync] failed: {ex}");
            return false;
        }
    }

    // ----------------- Helper -----------------
    private static UserDto ToUserDto(User user) => new()
    {
        UserId = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        DateOfBirth = user.DateOfBirth,
        AvatarUrl = user.AvatarUrl,
        PhoneNumber = user.Phone,
        RoleName = user.RoleName,
        CreatedAt = user.CreatedAt
    };
}