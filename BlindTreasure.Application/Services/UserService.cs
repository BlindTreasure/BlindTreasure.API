using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
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


    public async Task<UserDto?> GetUserDetailsByIdAsync(Guid userId)
    {
        _logger.Info($"[GetUserByIdAsync] Admin requests detail for user {userId}");

        var user = await GetUserById(userId, true);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[GetUserByIdAsync] User {userId} not found or deleted.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
        }

        return UserMapper.ToUserDto(user);
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
        return UserMapper.ToUserDto(user);
    }

    public async Task<UpdateAvatarResultDto?> UploadAvatarAsync(Guid userId, IFormFile file)
    {
        _logger.Info($"[UploadAvatarAsync] Bắt đầu cập nhật avatar cho user {userId}");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[UploadAvatarAsync] Không tìm thấy user {userId} hoặc đã bị xóa.");
            throw ErrorHelper.NotFound("Người dùng không tồn tại hoặc đã bị xóa.");
        }

        if (file == null || file.Length == 0)
        {
            _logger.Warn("[UploadAvatarAsync] File avatar không hợp lệ.");
            throw ErrorHelper.BadRequest("File ảnh không hợp lệ hoặc rỗng.");
        }

        // Sinh tên file duy nhất để tránh trùng (VD: avatar_userId_timestamp.png)
        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"avatars/avatar_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{fileExtension}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetPreviewUrlAsync(fileName);
        if (string.IsNullOrEmpty(fileUrl))
        {
            _logger.Error($"[UploadAvatarAsync] Không thể lấy URL cho file {fileName}");
            throw ErrorHelper.Internal("Không thể tạo URL cho ảnh đại diện.");
        }

        user.AvatarUrl = fileUrl;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Ghi cache theo email và id
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[UploadAvatarAsync] Đã cập nhật avatar thành công cho user {user.Email}");

        return new UpdateAvatarResultDto { AvatarUrl = fileUrl };
    }


    //Admin methods
    public async Task<Pagination<UserDto>> GetAllUsersAsync(UserQueryParameter param)
    {
        _logger.Info($"[GetAllUsersAsync] Admin requests user list. Page: {param.PageIndex}, Size: {param.PageSize}");

        if (param.PageIndex <= 0 || param.PageSize <= 0)
            throw ErrorHelper.BadRequest("Thông số phân trang không hợp lệ. PageIndex và PageSize phải lớn hơn 0.");

        var cacheKey =
            $"user:list:search={param.Search}-status={param.Status}-role={param.RoleName}-sort={param.SortBy}-desc={param.Desc}-page={param.PageIndex}-size={param.PageSize}";
        var cachedResult = await _cacheService.GetAsync<Pagination<UserDto>>(cacheKey);
        if (cachedResult != null)
        {
            _logger.Info($"[GetAllUsersAsync] Trả kết quả từ cache: {cacheKey}");
            return cachedResult;
        }

        var query = _unitOfWork.Users.GetQueryable()
            .Where(u => !u.IsDeleted)
            .AsNoTracking();

        // Search filter
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(u =>
                (!string.IsNullOrEmpty(u.FullName) && u.FullName.ToLower().Contains(keyword)) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(keyword)));
        }

        if (param.Status.HasValue)
            query = query.Where(u => u.Status == param.Status.Value);

        if (param.RoleName.HasValue)
            query = query.Where(u => u.RoleName == param.RoleName.Value);

        // Sort
        query = param.SortBy switch
        {
            UserSortField.Email => param.Desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            UserSortField.FullName => param.Desc
                ? query.OrderByDescending(u => u.FullName)
                : query.OrderBy(u => u.FullName),
            _ => param.Desc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt)
        };

        var total = await query.CountAsync();

        if (total == 0)
            throw ErrorHelper.NotFound("Không tìm thấy người dùng nào.");

        var users = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var userDtos = users.Select(UserMapper.ToUserDto).ToList();
        var result = new Pagination<UserDto>(userDtos, total, param.PageIndex, param.PageSize);

        // Cache trong 5 phút
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

        _logger.Info($"[GetAllUsersAsync] Đã lưu cache: {cacheKey}");
        return result;
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
        return UserMapper.ToUserDto(user);
    }

    public async Task<UserDto?> UpdateUserStatusAsync(Guid userId, UserStatus newStatus)
    {
        _logger.Info($"[UpdateUserStatusAsync] Admin updates status for user {userId} to {newStatus}");

        var user = await GetUserById(userId);
        if (user == null)
        {
            _logger.Warn($"[UpdateUserStatusAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại.");
        }

        user.Status = newStatus;

        // Nếu ban/deactive thì soft remove, nếu active lại thì mở lại
        if (newStatus == UserStatus.Suspended || newStatus == UserStatus.Locked)
            user.IsDeleted = true;
        else if (newStatus == UserStatus.Active)
            user.IsDeleted = false;

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[UpdateUserStatusAsync] User {user.Email} status updated to {newStatus} by admin.");
        return UserMapper.ToUserDto(user);
    }


    /// <summary>
    ///     Gets a user by id, optionally using cache.
    /// </summary>
    public async Task<User?> GetUserByEmail(string email, bool useCache = false)
    {
        if (useCache)
        {
            var cacheKey = $"user:{email}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
            if (cachedUser != null) return cachedUser;

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user != null)
                await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
            return user;
        }

        return await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
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
}