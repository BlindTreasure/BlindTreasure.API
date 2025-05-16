using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BlindTreasure.Application.Services;

/// <summary>
///     Service for authentication, registration, OTP, and password management.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(
        ILoggerService loggerService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
    }

    public async Task<UserDto?> RegisterUserAsync(UserRegistrationDto registrationDto)
    {
        try
        {
            _loggerService.Info($"[RegisterUserAsync] Start registration for {registrationDto.Email}");

            if (await UserExistsAsync(registrationDto.Email))
            {
                _loggerService.Warn($"[RegisterUserAsync] Email {registrationDto.Email} already registered.");
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

            _loggerService.Success($"[RegisterUserAsync] User {user.Email} created successfully.");

            await GenerateAndSendOtpAsync(user, OtpPurpose.Register, "register-otp");

            _loggerService.Info($"[RegisterUserAsync] OTP sent to {user.Email} for verification.");

            return ToUserDto(user);
        }
        catch (Exception ex)
        {

            _loggerService.Error($"[RegisterUserAsync] failed: {ex.Message}");
            throw new Exception($"[RegisterUserAsync] failed: {ex.Message}");
        }
    }


    /// <summary>
    ///     Authenticates a user and returns JWT tokens.
    /// </summary>
    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto, IConfiguration configuration)
    {
        try
        {
            _loggerService.Info($"[LoginAsync] Login attempt for {loginDto.Email}");

            // Get user from cache or DBB
            var user = await GetUserByEmailAsync(loginDto.Email!, true);
            if (user == null)
                throw ErrorHelper.NotFound("Tài khoản không tồn tại.");

            if (!new PasswordHasher().VerifyPassword(loginDto.Password!, user.Password))
                throw ErrorHelper.Unauthorized("Mật khẩu không chính xác.");

            if (user.Status == UserStatus.Pending)
                throw ErrorHelper.Forbidden("Tài khoản chưa được kích hoạt.");

            _loggerService.Success($"[LoginAsync] User {loginDto.Email} authenticated successfully.");

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

            _loggerService.Info($"[LoginAsync] Tokens generated and user cache updated for {user.Email}");

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }
        catch (Exception ex)
        {

            _loggerService.Error($"[LoginAsync] failed: {ex.Message}");
            throw new Exception($"[LoginAsync] failed: {ex.Message}");
        }
    }

    public async Task<UpdateAvatarResultDto?> UpdateAvatarAsync(Guid userId, IFormFile file)
    {
        try
        {
            _loggerService.Info($"[UpdateAvatarAsync] Update avatar for user {userId}");

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
            {
                _loggerService.Warn($"[UpdateAvatarAsync] User {userId} not found.");
                return null;
            }

            // Xử lý lưu file, ví dụ lưu vào wwwroot/avatars
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileExt = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExt}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Cập nhật đường dẫn avatar (giả sử dùng đường dẫn tĩnh)
            user.AvatarUrl = $"/avatars/{fileName}";

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));

            _loggerService.Success($"[UpdateAvatarAsync] Avatar updated for user {user.Email}");

            return new UpdateAvatarResultDto { AvatarUrl = user.AvatarUrl };
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[UpdateAvatarAsync] failed: {ex.Message}");
            throw ex;
        }
    }




    /// <summary>
    ///     Verifies the OTP for email registration and activates the user.
    /// </summary>
    public async Task<bool> VerifyEmailOtpAsync(string email, string otp)
    {
        try
        {
            _loggerService.Info($"[VerifyEmailOtpAsync] Verifying OTP for {email}");

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                _loggerService.Warn($"[VerifyEmailOtpAsync] User {email} not found.");
                return false;
            }

            if (user.IsEmailVerified)
            {
                _loggerService.Warn($"[VerifyEmailOtpAsync] User {email} already verified.");
                return false;
            }

            // Verify OTP (from cache or DB)
            if (!await VerifyOtpAsync(email, otp, OtpPurpose.Register, "register-otp"))
            {
                _loggerService.Warn($"[VerifyEmailOtpAsync] Invalid or expired OTP for {email}");
                return false;
            }

            // Activate user
            user.IsEmailVerified = true;
            user.Status = UserStatus.Active;

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"user:{email}", user, TimeSpan.FromHours(1));

            // Remove OTP from cache after successful verification
            await _cacheService.RemoveAsync($"register-otp:{email}");

            // Send registration success email
            await _emailService.SendRegistrationSuccessEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                UserName = user.FullName
            });

            _loggerService.Success($"[VerifyEmailOtpAsync] User {email} verified and activated.");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[VerifyEmailOtpAsync] failed: {ex.Message}");
            throw new Exception($"[VerifyEmailOtpAsync] failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Resends OTP for registration (with cooldown).
    /// </summary>
    public async Task<bool> ResendRegisterOtpAsync(string email)
    {
        try
        {
            _loggerService.Info($"[ResendRegisterOtpAsync] Resend OTP requested for {email}");

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                _loggerService.Warn($"[ResendRegisterOtpAsync] User {email} not found.");
                return false;
            }

            if (user.IsEmailVerified)
            {
                _loggerService.Warn($"[ResendRegisterOtpAsync] User {email} already verified.");
                return false;
            }

            // Check cooldown to prevent spam
            if (await _cacheService.ExistsAsync($"otp-sent:{email}"))
            {
                _loggerService.Warn($"[ResendRegisterOtpAsync] Cooldown active for {email}.");
                return false;
            }

            // Generate and send new OTP
            await GenerateAndSendOtpAsync(user, OtpPurpose.Register, "register-otp");
            await _cacheService.SetAsync($"otp-sent:{email}", true, TimeSpan.FromMinutes(1));

            _loggerService.Success($"[ResendRegisterOtpAsync] OTP resent to {email}.");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[ResendRegisterOtpAsync] failed: {ex.Message}");
            throw new Exception($"[ResendRegisterOtpAsync] failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Sends or resends OTP for forgot password (with cooldown).
    /// </summary>
    public async Task<bool> SendForgotPasswordOtpRequestAsync(string email)
    {
        try
        {
            _loggerService.Info($"[SendForgotPasswordOtpRequestAsync] Request for {email}");

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                _loggerService.Warn($"[SendForgotPasswordOtpRequestAsync] User {email} not found.");
                return false;
            }

            if (!user.IsEmailVerified)
            {
                _loggerService.Warn($"[SendForgotPasswordOtpRequestAsync] User {email} not verified.");
                return false;
            }

            // Check cooldown to prevent spam
            if (await _cacheService.ExistsAsync($"forgot-otp-sent:{email}"))
            {
                _loggerService.Warn($"[SendForgotPasswordOtpRequestAsync] Cooldown active for {email}.");
                return false;
            }

            // Generate and send OTP for forgot password
            await GenerateAndSendOtpAsync(user, OtpPurpose.ForgotPassword, "forgot-otp");
            await _cacheService.SetAsync($"forgot-otp-sent:{email}", true, TimeSpan.FromMinutes(1));

            _loggerService.Success($"[SendForgotPasswordOtpRequestAsync] Forgot password OTP sent to {email}.");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[SendForgotPasswordOtpRequestAsync] failed: {ex.Message}");
            throw new Exception($"[SendForgotPasswordOtpRequestAsync] failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Resets the user's password after verifying OTP.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        try
        {
            _loggerService.Info($"[ResetPasswordAsync] Password reset requested for {email}");

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                _loggerService.Warn($"[ResetPasswordAsync] User {email} not found.");
                return false;
            }

            if (!user.IsEmailVerified)
            {
                _loggerService.Warn($"[ResetPasswordAsync] User {email} not verified.");
                return false;
            }

            // Verify OTP (from cache or DB)
            if (!await VerifyOtpAsync(email, otp, OtpPurpose.ForgotPassword, "forgot-otp"))
            {
                _loggerService.Warn($"[ResetPasswordAsync] Invalid or expired OTP for {email}");
                return false;
            }

            // Hash and update new password
            user.Password = new PasswordHasher().HashPassword(newPassword);

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"user:{email}", user, TimeSpan.FromHours(1));

            // Remove OTP from cache after successful verification
            await _cacheService.RemoveAsync($"forgot-otp:{email}");

            // Send password change notification
            await _emailService.SendPasswordChangeEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                UserName = user.FullName
            });

            _loggerService.Success($"[ResetPasswordAsync] Password reset successful for {email}.");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[ResetPasswordAsync] failed: {ex.Message}");
            return false;
        }
    }


    public async Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        try
        {
            _loggerService.Info($"[UpdateProfileAsync] Update profile for user {userId}");

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
            {
                _loggerService.Warn($"[UpdateProfileAsync] User {userId} not found.");
                return null;
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

            _loggerService.Success($"[UpdateProfileAsync] Profile updated for user {user.Email}");
            return ToUserDto(user);
        }
        catch (Exception ex)
        {
            _loggerService.Error($"[UpdateProfileAsync] failed: {ex.Message}");
            return null;
        }
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
            _loggerService.Info($"[GenerateAndSendOtpAsync] Registration OTP sent to {user.Email}");
        }
        else if (purpose == OtpPurpose.ForgotPassword)
        {
            await _emailService.SendForgotPasswordOtpEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                Otp = otpToken.Code,
                UserName = user.FullName
            });
            _loggerService.Info($"[GenerateAndSendOtpAsync] Forgot password OTP sent to {user.Email}");
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
                _loggerService.Warn($"[VerifyOtpAsync] OTP mismatch for {email} (purpose: {purpose})");
                return false;
            }

            // Remove OTP from cache after successful verification
            await _cacheService.RemoveAsync(cacheKey);
            _loggerService.Info(
                $"[VerifyOtpAsync] OTP for {email} (purpose: {purpose}) verified and removed from cache.");
            return true;
        }

        // Fallback: check in DB if not found in cache
        var otpRecord = await _unitOfWork.OtpVerifications.FirstOrDefaultAsync(o =>
            o.Target == email && o.OtpCode == otp && o.Purpose == purpose && !o.IsUsed);

        if (otpRecord == null || otpRecord.ExpiredAt < DateTime.UtcNow)
        {
            _loggerService.Warn($"[VerifyOtpAsync] OTP not found or expired for {email} (purpose: {purpose})");
            return false;
        }

        otpRecord.IsUsed = true;
        await _unitOfWork.OtpVerifications.Update(otpRecord);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.Info(
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