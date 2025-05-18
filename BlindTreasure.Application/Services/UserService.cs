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

public class UserService : IUserService
{
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;

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

    public async Task<Pagination<UserDto>> GetAllUsersAsync(PaginationParameter param)
    {
        _logger.Info($"[GetAllUsersAsync] Admin requests user list. Page: {param.PageIndex}, Size: {param.PageSize}");

        // Validate input
        if (param.PageIndex <= 0 || param.PageSize <= 0)
            throw ErrorHelper.BadRequest("Thông số phân trang không hợp lệ. PageIndex và PageSize phải lớn hơn 0.");

        var query = _unitOfWork.Users.GetQueryable().Where(u => !u.IsDeleted)
            .AsNoTracking();

        var count = await query.CountAsync();

        if (count == 0)
            throw ErrorHelper.NotFound("Không tìm thấy người dùng nào.");

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        if (users == null || !users.Any())
            throw ErrorHelper.NotFound("Trang hiện tại không có người dùng nào.");

        var userDtos = users.Select(ToUserDto).ToList();
        var result = new Pagination<UserDto>(userDtos, count, param.PageIndex, param.PageSize);

        return result;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        _logger.Info($"[GetUserByIdAsync] Admin requests detail for user {userId}");

        var user = await GetUserById(userId, true);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[GetUserByIdAsync] User {userId} not found or deleted.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
        }

        return ToUserDto(user);
    }

    public async Task<UserDto?> CreateUserAsync(UserCreateDto dto)
    {
        _logger.Info($"[CreateUserAsync] Admin creates user {dto.Email}");

        if (await UserExistsAsync(dto.Email))
        {
            _logger.Warn($"[CreateUserAsync] Email {dto.Email} already exists.");
            throw ErrorHelper.Conflict($"Email {dto.Email} đã tồn tại trong hệ thống.");
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
            IsEmailVerified = true
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[CreateUserAsync] User {user.Email} created by admin.");
        return ToUserDto(user);
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UserUpdateDto dto)
    {
        _logger.Info($"[UpdateUserAsync] Admin updates user {userId}");

        var user = await GetUserById(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[UpdateUserAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
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

    public async Task<UpdateAvatarResultDto?> UpdateUserAvatarAsync(Guid userId, IFormFile file)
    {
        _logger.Info($"[UpdateUserAvatarAsync] Admin updates avatar for user {userId}");

        var user = await GetUserById(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[UpdateUserAvatarAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
        }

        if (file == null || file.Length == 0)
        {
            throw ErrorHelper.BadRequest("File ảnh không hợp lệ hoặc rỗng.");
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


    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        _logger.Info($"[DeleteUserAsync] Admin deletes user {userId}");

        var user = await GetUserById(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[DeleteUserAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
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

    public async Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        _logger.Info($"[UpdateProfileAsync] Update profile for user {userId}");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[UpdateProfileAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
        }

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName;
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            user.Phone = dto.PhoneNumber;
        if (dto.DateOfBirth.HasValue)
            user.DateOfBirth = dto.DateOfBirth.Value;
        if (dto.Gender.HasValue)
            user.Gender = dto.Gender.Value;

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[UpdateProfileAsync] Profile updated for user {user.Email}");
        return ToUserDto(user);
    }

    public async Task<UpdateAvatarResultDto?> UpdateAvatarAsync(Guid userId, IFormFile file)
    {
        _logger.Info($"[UpdateAvatarAsync] Update avatar for user {userId}");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[UpdateAvatarAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
        }

        if (file == null || file.Length == 0)
        {
            _logger.Warn($"[UpdateAvatarAsync] Invalid file upload for user {userId}.");
            throw ErrorHelper.BadRequest("File ảnh không hợp lệ hoặc rỗng.");
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

        _logger.Success($"[UpdateAvatarAsync] Avatar updated for user {user.Email}");

        return new UpdateAvatarResultDto { AvatarUrl = user.AvatarUrl };
    }


// ----------------- PRIVATE HELPER METHODS -----------------

    /// <summary>
    ///     Checks if a user exists in cache or DB.
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
    ///     Gets a user by id, optionally using cache.
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
    ///     Maps User entity to UserDto.
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