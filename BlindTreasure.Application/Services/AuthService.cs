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
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(
        ILoggerService logger,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
    }

    public async Task<UserDto?> RegisterUserAsync(UserRegistrationDto registrationDto)
    {
        _logger.Info($"[RegisterUserAsync] Start registration for {registrationDto.Email}");

        if (await UserExistsAsync(registrationDto.Email))
        {
            _logger.Warn($"[RegisterUserAsync] Email {registrationDto.Email} already registered.");
            throw ErrorHelper.Conflict("Email đã được sử dụng.");
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

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register, "register-otp");

        _logger.Info($"[RegisterUserAsync] OTP sent to {user.Email} for verification.");

        return ToUserDto(user);
    }


    /// <summary>
    ///     Authenticates a user and returns JWT tokens.
    /// </summary>
    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto, IConfiguration configuration)
    {
        _logger.Info($"[LoginAsync] Login attempt for {loginDto.Email}");

        // Get user from cache or DBB
        var user = await GetUserByEmailAsync(loginDto.Email!, true);
        if (user == null)
            throw ErrorHelper.NotFound("Tài khoản không tồn tại.");

        if (!new PasswordHasher().VerifyPassword(loginDto.Password!, user.Password))
            throw ErrorHelper.Unauthorized("Mật khẩu không chính xác.");

        if (user.Status != UserStatus.Active)
            throw ErrorHelper.Forbidden("Tài khoản chưa được kích hoạt.");

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
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));

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
            throw ErrorHelper.NotFound("Tài khoản không tồn tại.");

        if (user.IsDeleted || user.Status == UserStatus.Suspended)
            throw ErrorHelper.Forbidden("Tài khoản đã bị vô hiệu hóa hoặc cấm.");

        if (string.IsNullOrEmpty(user.RefreshToken))
            throw ErrorHelper.BadRequest("Người dùng đã đăng xuất trước đó.");

        // Xóa token trong DB
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // ——> Xóa cache user sau khi DB đã commit
        await _cacheService.RemoveAsync($"user:{user.Email}");

        _logger.Info($"[LogoutAsync] Logout successful for user ID: {userId}.");
        return true;
    }

    public async Task<LoginResponseDto?> RefreshTokenAsync(TokenRefreshRequestDto refreshTokenDto,
        IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenDto.RefreshToken)) throw ErrorHelper.BadRequest("Thiếu token");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == refreshTokenDto.RefreshToken);

        if (user == null)
            throw ErrorHelper.NotFound("Tài khoản không tồn tại.");

        if (string.IsNullOrEmpty(user.RefreshToken))
            throw ErrorHelper.BadRequest("Người dùng đã đăng xuất trước đó.");

        // Kiểm tra Refresh Token có hợp lệ không (thời gian hết hạn)
        if (user.RefreshTokenExpiryTime < DateTime.UtcNow)
            throw ErrorHelper.Conflict("Refresh token has expired.");

        var roleName = user.RoleName.ToString();

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


        return new LoginResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
    }

    /// <summary>
    ///     Verifies the OTP for email registration and activates the user.
    /// </summary>
    public async Task<bool> VerifyEmailOtpAsync(string email, string otp)
    {
        _logger.Info($"[VerifyEmailOtpAsync] Verifying OTP for {email}");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) throw ErrorHelper.NotFound("Tài khoản không tồn tại.");

        if (user.IsEmailVerified) return false;
        if (!await VerifyOtpAsync(email, otp, OtpPurpose.Register, "register-otp"))
            return false;

        // Activate user
        user.IsEmailVerified = true;
        user.Status = UserStatus.Active;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // ——> Xóa cache cũ rồi thiết lập lại cache user mới
        await _cacheService.RemoveAsync($"user:{email}");
        await _cacheService.SetAsync($"user:{email}", user, TimeSpan.FromHours(1));

        // Xóa OTP khỏi cache
        await _cacheService.RemoveAsync($"register-otp:{email}");

        await _emailService.SendRegistrationSuccessEmailAsync(new EmailRequestDto
        {
            To = user.Email,
            UserName = user.FullName
        });

        _logger.Success($"[VerifyEmailOtpAsync] User {email} verified and activated.");
        return true;
    }


    /// <summary>
    ///     Resends OTP for registration (with cooldown).
    /// </summary>
    public async Task<bool> ResendRegisterOtpAsync(string email)
    {
        try
        {
            _logger.Info($"[ResendRegisterOtpAsync] Resend OTP requested for {email}");

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                _logger.Warn($"[ResendRegisterOtpAsync] User {email} not found.");
                return false;
            }

            if (user.IsEmailVerified)
            {
                _logger.Warn($"[ResendRegisterOtpAsync] User {email} already verified.");
                return false;
            }

            // Check cooldown to prevent spam
            if (await _cacheService.ExistsAsync($"otp-sent:{email}"))
            {
                _logger.Warn($"[ResendRegisterOtpAsync] Cooldown active for {email}.");
                return false;
            }

            // Generate and send new OTP
            await GenerateAndSendOtpAsync(user, OtpPurpose.Register, "register-otp");
            await _cacheService.SetAsync($"otp-sent:{email}", true, TimeSpan.FromMinutes(1));

            _logger.Success($"[ResendRegisterOtpAsync] OTP resent to {email}.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[ResendRegisterOtpAsync] failed: {ex}");
            return false;
        }
    }

    /// <summary>
    ///     Sends or resends OTP for forgot password (with cooldown).
    /// </summary>
    public async Task<bool> SendForgotPasswordOtpRequestAsync(string email)
    {
        try
        {
            _logger.Info($"[SendForgotPasswordOtpRequestAsync] Request for {email}");

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                _logger.Warn($"[SendForgotPasswordOtpRequestAsync] User {email} not found.");
                return false;
            }

            if (!user.IsEmailVerified)
            {
                _logger.Warn($"[SendForgotPasswordOtpRequestAsync] User {email} not verified.");
                return false;
            }

            // Check cooldown to prevent spam
            if (await _cacheService.ExistsAsync($"forgot-otp-sent:{email}"))
            {
                _logger.Warn($"[SendForgotPasswordOtpRequestAsync] Cooldown active for {email}.");
                return false;
            }

            // Generate and send OTP for forgot password
            await GenerateAndSendOtpAsync(user, OtpPurpose.ForgotPassword, "forgot-otp");
            await _cacheService.SetAsync($"forgot-otp-sent:{email}", true, TimeSpan.FromMinutes(1));

            _logger.Success($"[SendForgotPasswordOtpRequestAsync] Forgot password OTP sent to {email}.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[SendForgotPasswordOtpRequestAsync] failed: {ex}");
            return false;
        }
    }

    /// <summary>
    ///     Resets the user's password after verifying OTP.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        _logger.Info($"[ResetPasswordAsync] Password reset requested for {email}");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        if (user == null) return false;
        if (!user.IsEmailVerified) return false;
        if (!await VerifyOtpAsync(email, otp, OtpPurpose.ForgotPassword, "forgot-otp"))
            return false;

        // Hash và cập nhật mật khẩu
        user.Password = new PasswordHasher().HashPassword(newPassword);
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // ——> Xóa cache cũ rồi set lại cache user với mật khẩu mới
        await _cacheService.RemoveAsync($"user:{email}");
        await _cacheService.SetAsync($"user:{email}", user, TimeSpan.FromHours(1));

        // Xóa OTP khỏi cache
        await _cacheService.RemoveAsync($"forgot-otp:{email}");

        await _emailService.SendPasswordChangeEmailAsync(new EmailRequestDto
        {
            To = user.Email,
            UserName = user.FullName
        });

        _logger.Success($"[ResetPasswordAsync] Password reset successful for {email}.");
        return true;
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
    ///     Gets a user by email, optionally using cache.
    /// </summary>
    private async Task<User?> GetUserByEmailAsync(string email, bool useCache = false)
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

        return await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    ///     Gets a user by id
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
    ///     Generates an OTP, saves it to DB and cache, and sends the appropriate email.
    /// </summary>
    private async Task GenerateAndSendOtpAsync(User user, OtpPurpose purpose, string otpCachePrefix)
    {
        var otpToken = OtpGenerator.GenerateToken(6, TimeSpan.FromMinutes(10));
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
        await _cacheService.SetAsync($"{otpCachePrefix}:{user.Email}", otpToken.Code, TimeSpan.FromMinutes(10));

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
    private async Task<bool> VerifyOtpAsync(string email, string otp, OtpPurpose purpose, string otpCachePrefix)
    {
        var cacheKey = $"{otpCachePrefix}:{email}";
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
            PhoneNumber = user.Phone,
            RoleName = user.RoleName,
            CreatedAt = user.CreatedAt
        };
    }
}