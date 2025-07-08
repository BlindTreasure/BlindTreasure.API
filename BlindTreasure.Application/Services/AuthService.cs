using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BlindTreasure.Application.Services;

/// <summary>
///     Service for authentication, registration, OTP, and password management.
/// </summary>
public class AuthService : IAuthService
{
    // Cache key constants for better maintainability
    private static class CacheKeys
    {
        public static string User(string email) => $"user:{email}";
        public static string User(Guid id) => $"user:{id}";
        public static string RefreshToken(string token) => $"refresh:{token}";
        public static string RegisterOtp(string email) => $"register-otp:{email}";
        public static string ForgotOtp(string email) => $"forgot-otp:{email}";
        public static string OtpSent(string email) => $"otp-sent:{email}";
        public static string OtpCounter(string email) => $"forgot-otp-count:{email}";
    }

    // Cache TTL constants
    private static class CacheTTL
    {
        public static readonly TimeSpan User = TimeSpan.FromHours(1);
        public static readonly TimeSpan RefreshToken = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan Otp = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan OtpCooldown = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan OtpCounter = TimeSpan.FromMinutes(15);
    }

    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _logger;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;


    public AuthService(
        ILoggerService logger,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService, INotificationService notificationService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
        _notificationService = notificationService;
    }

    #region Authen

    public async Task<UserDto?> RegisterCustomerAsync(UserRegistrationDto registrationDto)
    {
        _logger.Info($"[RegisterUserAsync] Start registration for {registrationDto.Email}");

        if (await UserExistsAsync(registrationDto.Email))
        {
            _logger.Warn($"[RegisterUserAsync] Email {registrationDto.Email} already registered.");
            throw ErrorHelper.Conflict(ErrorMessages.AccountEmailAlreadyRegistered);
        }

        var hashedPassword = new PasswordHasher().HashPassword(registrationDto.Password);

        var user = new User
        {
            Email = registrationDto.Email,
            Password = hashedPassword,
            FullName = registrationDto.FullName,
            Phone = registrationDto.PhoneNumber,
            DateOfBirth = registrationDto.DateOfBirth,
            AvatarUrl = "https://img.freepik.com/free-psd/3d-illustration-human-avatar-profile_23-2150671142.jpg",
            Status = UserStatus.Pending,
            RoleName = RoleType.Customer,
            IsEmailVerified = false
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.Success($"[RegisterUserAsync] User {user.Email} created successfully.");

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register, CacheKeys.RegisterOtp(user.Email));

        _logger.Info($"[RegisterUserAsync] OTP sent to {user.Email} for verification.");

        // Cache new user after creation
        await UpdateUserCacheAsync(user);

        return ToUserDto(user);
    }

    public async Task<UserDto?> RegisterSellerAsync(SellerRegistrationDto dto)
    {
        if (await UserExistsAsync(dto.Email))
            throw ErrorHelper.Conflict(ErrorMessages.AccountEmailAlreadyRegistered);

        var hashedPassword = new PasswordHasher().HashPassword(dto.Password);

        var user = new User
        {
            Email = dto.Email,
            Password = hashedPassword,
            FullName = dto.FullName,
            Phone = dto.PhoneNumber,
            DateOfBirth = dto.DateOfBirth,
            AvatarUrl = "https://img.freepik.com/free-psd/3d-illustration-human-avatar-profile_23-2150671142.jpg",
            RoleName = RoleType.Seller,
            Status = UserStatus.Pending,
            IsEmailVerified = false
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var seller = new Seller
        {
            UserId = user.Id,
            CoaDocumentUrl = "Waiting for submit",
            CompanyName = dto.CompanyName,
            TaxId = dto.TaxId,
            CompanyAddress = dto.CompanyAddress,
            IsVerified = false,
            Status = SellerStatus.InfoEmpty
        };

        await _unitOfWork.Sellers.AddAsync(seller);
        await _unitOfWork.SaveChangesAsync();

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register, CacheKeys.RegisterOtp(user.Email));

        // Cache new user after creation
        await UpdateUserCacheAsync(user);

        return ToUserDto(user);
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto, IConfiguration configuration)
    {
        _logger.Info($"[LoginAsync] Login attempt for {loginDto.Email}");

        // Get user from cache or DB
        var user = await GetUserByEmailAsync(loginDto.Email!, true);
        if (user == null)
            throw ErrorHelper.NotFound(ErrorMessages.AccountNotFound);

        if (!new PasswordHasher().VerifyPassword(loginDto.Password!, user.Password))
            throw ErrorHelper.Unauthorized(ErrorMessages.AccountWrongPassword);

        if (user.Status != UserStatus.Active)
            throw ErrorHelper.Forbidden(ErrorMessages.AccountNotVerified);

        _logger.Success($"[LoginAsync] User {loginDto.Email} authenticated successfully.");

        var accessToken = JwtUtils.GenerateJwtToken(
            user.Id,
            user.Email,
            user.RoleName.ToString(),
            configuration,
            TimeSpan.FromMinutes(30)
        );

        var refreshToken = Guid.NewGuid().ToString();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
        
        // Update user in cache with new refresh token
        await UpdateUserCacheAsync(user);

        // Sau khi xác thực thành công
        await _notificationService.SendNotificationToUserAsync(
            user.Id,
            "Chào mừng!",
            $"Chào mừng {user.FullName} quay trở lại BlindTreasure.",
            NotificationType.System,
            TimeSpan.FromHours(1) // chỉ gửi 1 lần mỗi giờ
        );

        _logger.Info($"[LoginAsync] Tokens generated and user cache updated for {user.Email}");

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = ToUserDto(user)
        };
    }

    public async Task<bool> LogoutAsync(Guid userId)
    {
        _logger.Info($"[LogoutAsync] Logout process initiated for user ID: {userId}");

        var user = await GetUserById(userId);
        if (user == null)
            throw ErrorHelper.NotFound(ErrorMessages.AccountNotFound);

        if (user.IsDeleted || user.Status == UserStatus.Suspended)
            throw ErrorHelper.Forbidden(ErrorMessages.AccountSuspendedOrBan);

        if (string.IsNullOrEmpty(user.RefreshToken))
            throw ErrorHelper.BadRequest(ErrorMessages.AccountAccesstokenInvalid);

        // Get old refresh token before nullifying it for cache invalidation
        var oldRefreshToken = user.RefreshToken;
            
        // Xóa token trong DB
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate all user-related caches
        await InvalidateUserCacheAsync(user);

        _logger.Info($"[LogoutAsync] Logout successful for user ID: {userId}.");
        return true;
    }

    public async Task<LoginResponseDto?> RefreshTokenAsync(TokenRefreshRequestDto refreshTokenDto,
        IConfiguration configuration)
    {
        // Try to get user from refresh token cache first
        var cacheKey = CacheKeys.RefreshToken(refreshTokenDto.RefreshToken);
        var user = await _cacheService.GetAsync<User>(cacheKey);

        if (user == null)
        {
            user = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == refreshTokenDto.RefreshToken);

            if (user == null)
                throw ErrorHelper.NotFound(ErrorMessages.AccountNotFound);

            // Cache user by refresh token if valid
            if (!string.IsNullOrEmpty(user.RefreshToken) && user.RefreshTokenExpiryTime >= DateTime.UtcNow)
                await _cacheService.SetAsync(cacheKey, user, CacheTTL.RefreshToken);
        }

        if (string.IsNullOrEmpty(user.RefreshToken))
            throw ErrorHelper.BadRequest(ErrorMessages.AccountAccesstokenInvalid);

        if (user.RefreshTokenExpiryTime < DateTime.UtcNow)
            throw ErrorHelper.Conflict(ErrorMessages.Jwt_RefreshTokenExpired);

        var roleName = user.RoleName.ToString();
        var oldRefreshToken = user.RefreshToken;
        
        var newAccessToken = JwtUtils.GenerateJwtToken(
            user.Id,
            user.Email,
            roleName,
            configuration,
            TimeSpan.FromHours(1)
        );

        var newRefreshToken = Guid.NewGuid().ToString();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Update all caches with new user data
        await UpdateUserCacheAsync(user);
        
        // Invalidate old refresh token cache
        await _cacheService.RemoveAsync(CacheKeys.RefreshToken(oldRefreshToken));

        return new LoginResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
    }

    #endregion

    #region Otp & Emails

    public async Task<bool> VerifyEmailOtpAsync(string email, string otp)
    {
        _logger.Info($"[VerifyEmailOtpAsync] Verifying OTP for {email}");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) throw ErrorHelper.NotFound(ErrorMessages.AccountNotFound);

        if (user.IsEmailVerified) return false;
        if (!await VerifyOtpAsync(email, otp, OtpPurpose.Register, CacheKeys.RegisterOtp(email)))
            return false;

        // Activate user
        user.IsEmailVerified = true;
        user.Status = UserStatus.Active;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Update cache with verified user
        await UpdateUserCacheAsync(user);

        // Gửi email tùy role
        switch (user.RoleName)
        {
            case RoleType.Customer:
                await _emailService.SendRegistrationSuccessEmailAsync(new EmailRequestDto
                {
                    To = user.Email,
                    UserName = user.FullName
                });
                break;

            case RoleType.Seller:
                await _emailService.SendSellerEmailVerificationSuccessAsync(new EmailRequestDto
                {
                    To = user.Email,
                    UserName = user.FullName
                });
                break;

            default:
                // Nếu cần, có thể log hoặc xử lý role khác
                _logger.Warn($"[VerifyEmailOtpAsync] Role {user.RoleName} không gửi email tự động.");
                break;
        }

        _logger.Success($"[VerifyEmailOtpAsync] User {email} verified and activated.");
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        _logger.Info($"[ResetPasswordAsync] Password reset requested for {email}");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        if (user == null) return false;
        if (!user.IsEmailVerified) return false;
        if (!await VerifyOtpAsync(email, otp, OtpPurpose.ForgotPassword, CacheKeys.ForgotOtp(email)))
            return false;

        // Hash và cập nhật mật khẩu
        user.Password = new PasswordHasher().HashPassword(newPassword);
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Update user in all caches with new password
        await UpdateUserCacheAsync(user);

        await _emailService.SendPasswordChangeEmailAsync(new EmailRequestDto
        {
            To = user.Email,
            UserName = user.FullName
        });

        _logger.Success($"[ResetPasswordAsync] Password reset successful for {email}.");
        return true;
    }

    public async Task<bool> ResendOtpAsync(string email, OtpType type)
    {
        return type switch
        {
            OtpType.Register => await ResendRegisterOtpAsync(email),
            OtpType.ForgotPassword => await SendForgotPasswordOtpRequestAsync(email),
            _ => throw ErrorHelper.BadRequest(ErrorMessages.Oauth_InvalidOtp)
        };
    }

    private async Task<bool> ResendRegisterOtpAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw ErrorHelper.NotFound(ErrorMessages.AccountNotFound);

        if (user.IsDeleted || user.Status == UserStatus.Suspended)
            throw ErrorHelper.Forbidden(ErrorMessages.AccountSuspendedOrBan);

        if (user.IsEmailVerified)
            throw ErrorHelper.Conflict(ErrorMessages.AccountAlreadyVerified);

        var cooldownKey = CacheKeys.OtpSent(email);
        if (await _cacheService.ExistsAsync(cooldownKey))
            throw ErrorHelper.BadRequest(ErrorMessages.VerifyOtpExistingCoolDown);

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register, CacheKeys.RegisterOtp(email));
        await _cacheService.SetAsync(cooldownKey, true, CacheTTL.OtpCooldown);

        return true;
    }

    private async Task<bool> SendForgotPasswordOtpRequestAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        if (user == null)
            throw ErrorHelper.NotFound(ErrorMessages.AccountNotFound);

        if (user.Status == UserStatus.Suspended)
            throw ErrorHelper.Forbidden(ErrorMessages.AccountSuspendedOrBan);

        if (!user.IsEmailVerified)
            throw ErrorHelper.Conflict(ErrorMessages.AccountNotVerified);

        var counterKey = CacheKeys.OtpCounter(email);
        var countValue = await _cacheService.GetAsync<int?>(counterKey) ?? 0;

        if (countValue >= 3)
            throw ErrorHelper.BadRequest(ErrorMessages.Oauth_InvalidOtp);

        // Gửi OTP
        await GenerateAndSendOtpAsync(user, OtpPurpose.ForgotPassword, CacheKeys.ForgotOtp(email));

        // Tăng số lần gửi và set timeout
        await _cacheService.SetAsync(counterKey, countValue + 1, CacheTTL.OtpCounter);

        _logger.Info($"[SendForgotPasswordOtpRequestAsync] OTP sent to {email}");

        return true;
    }

    #endregion

    #region PRIVATE HELPER METHODS

    /// <summary>
    /// Updates user data in all related caches
    /// </summary>
    private async Task UpdateUserCacheAsync(User user)
    {
        await Task.WhenAll(
            _cacheService.SetAsync(CacheKeys.User(user.Email), user, CacheTTL.User),
            _cacheService.SetAsync(CacheKeys.User(user.Id), user, CacheTTL.User)
        );
        
        // Also update refresh token cache if applicable
        if (!string.IsNullOrEmpty(user.RefreshToken) && user.RefreshTokenExpiryTime > DateTime.UtcNow)
        {
            await _cacheService.SetAsync(CacheKeys.RefreshToken(user.RefreshToken), user, CacheTTL.RefreshToken);
        }
    }
    
    /// <summary>
    /// Removes user data from all related caches
    /// </summary>
    private async Task InvalidateUserCacheAsync(User user)
    {
        await Task.WhenAll(
            _cacheService.InvalidateEntityCacheAsync<User>("user", user.Email),
            _cacheService.InvalidateEntityCacheAsync<User>("user", user.Id.ToString())
        );
        
        // Also invalidate refresh token cache if applicable
        if (!string.IsNullOrEmpty(user.RefreshToken))
        {
            await _cacheService.RemoveAsync(CacheKeys.RefreshToken(user.RefreshToken));
        }
    }

    /// <summary>
    ///     Checks if a user exists in cache or DB.
    /// </summary>
    private async Task<bool> UserExistsAsync(string email)
    {
        var cacheKey = CacheKeys.User(email);
        var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
        if (cachedUser != null) return true;

        var existingUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        return existingUser != null;
    }

    /// <summary>
    ///     Gets a user by email, optionally using cache.
    /// </summary>
    private async Task<User?> GetUserByEmailAsync(string email, bool useCache = false)
    {
        if (useCache)
        {
            var cacheKey = CacheKeys.User(email);
            return await _cacheService.GetOrRefreshAsync(
                cacheKey,
                async () => await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted),
                CacheTTL.User);
        }

        return await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    ///     Gets a user by id
    /// </summary>
    private async Task<User?> GetUserById(Guid id, bool useCache = false)
    {
        if (useCache)
        {
            var cacheKey = CacheKeys.User(id);
            return await _cacheService.GetOrRefreshAsync(
                cacheKey,
                async () => await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted),
                CacheTTL.User);
        }

        return await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    ///     Generates an OTP, saves it to DB and cache, and sends the appropriate email.
    /// </summary>
    private async Task GenerateAndSendOtpAsync(User user, OtpPurpose purpose, string cacheKey)
    {
        var otpToken = OtpGenerator.GenerateToken(6, CacheTTL.Otp);
        var otp = new OtpVerification
        {
            Target = user.Email,
            OtpCode = otpToken.Code,
            ExpiredAt = otpToken.ExpiresAtUtc,
            IsUsed = false,
            Purpose = purpose
        };

        await _unitOfWork.OtpVerifications.AddAsync(otp);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.SetAsync(cacheKey, otpToken.Code, CacheTTL.Otp);

        // Send the correct email based on OTP purpose
        if (purpose == OtpPurpose.Register)
        {
            await _emailService.SendOtpVerificationEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                Otp = otpToken.Code,
                UserName = user.FullName
            });
            _logger.Info($"[GenerateAndSendOtpAsync] Registration OTP sent to {user.Email}");
        }
        else if (purpose == OtpPurpose.ForgotPassword)
        {
            await _emailService.SendForgotPasswordOtpEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                Otp = otpToken.Code,
                UserName = user.FullName
            });
            _logger.Info($"[GenerateAndSendOtpAsync] Forgot password OTP sent to {user.Email}");
        }
    }

    /// <summary>
    ///     Verifies the OTP from cache or DB. Removes OTP from cache after successful verification.
    /// </summary>
    private async Task<bool> VerifyOtpAsync(string email, string otp, OtpPurpose purpose, string cacheKey)
    {
        var cachedOtp = await _cacheService.GetAsync<string>(cacheKey);
        if (cachedOtp != null)
        {
            if (cachedOtp != otp)
            {
                _logger.Warn($"[VerifyOtpAsync] OTP mismatch for {email} (purpose: {purpose})");
                return false;
            }

            // Remove OTP from cache after successful verification
            await _cacheService.RemoveAsync(cacheKey);
            _logger.Info(
                $"[VerifyOtpAsync] OTP for {email} (purpose: {purpose}) verified and removed from cache.");
            return true;
        }

        // Fallback: check in DB if not found in cache
        var otpRecord = await _unitOfWork.OtpVerifications.FirstOrDefaultAsync(o =>
            o.Target == email && o.OtpCode == otp && o.Purpose == purpose && !o.IsUsed);

        if (otpRecord == null || otpRecord.ExpiredAt < DateTime.UtcNow)
        {
            _logger.Warn($"[VerifyOtpAsync] OTP not found or expired for {email} (purpose: {purpose})");
            return false;
        }

        otpRecord.IsUsed = true;
        await _unitOfWork.OtpVerifications.Update(otpRecord);
        await _unitOfWork.SaveChangesAsync();
        _logger.Info(
            $"[VerifyOtpAsync] OTP for {email} (purpose: {purpose}) verified and marked as used in DB.");
        return true;
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
            Status = user.Status,
            PhoneNumber = user.Phone,
            RoleName = user.RoleName,
            CreatedAt = user.CreatedAt
        };
    }

    #endregion
}