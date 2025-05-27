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

    public async Task<UserDto?> RegisterCustomerAsync(UserRegistrationDto registrationDto)
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

    public async Task<UserDto?> RegisterSellerAsync(SellerRegistrationDto dto)
    {
        if (await UserExistsAsync(dto.Email))
            throw ErrorHelper.Conflict("Email đã được sử dụng.");

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
            CoaDocumentUrl = dto.CoaDocumentUrl,
            CompanyName = dto.CompanyName,
            TaxId = dto.TaxId,
            CompanyAddress = dto.CompanyAddress,
            IsVerified = false,
            Status = SellerStatus.WaitingReview
        };

        await _unitOfWork.Sellers.AddAsync(seller);
        await _unitOfWork.SaveChangesAsync();

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register, "register-otp");

        return ToUserDto(user);
    }

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

        // Xóa cache cũ rồi thiết lập lại cache user mới
        await _cacheService.RemoveAsync($"user:{email}");
        await _cacheService.SetAsync($"user:{email}", user, TimeSpan.FromHours(1));

        // Xóa OTP khỏi cache
        await _cacheService.RemoveAsync($"register-otp:{email}");

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

    public async Task<bool> ResendOtpAsync(string email, OtpType type)
    {
        return type switch
        {
            OtpType.Register => await ResendRegisterOtpAsync(email),
            OtpType.ForgotPassword => await SendForgotPasswordOtpRequestAsync(email),
            _ => throw ErrorHelper.BadRequest("Loại OTP không hợp lệ.")
        };
    }

    private async Task<bool> ResendRegisterOtpAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw ErrorHelper.NotFound("Email không tồn tại trong hệ thống.");

        if (user.IsDeleted || user.Status == UserStatus.Suspended)
            throw ErrorHelper.Forbidden("Tài khoản đã bị vô hiệu hóa hoặc cấm.");

        if (user.IsEmailVerified)
            throw ErrorHelper.Conflict("Tài khoản đã xác minh, không cần gửi lại OTP.");

        if (await _cacheService.ExistsAsync($"otp-sent:{email}"))
            throw ErrorHelper.BadRequest("Bạn đang gửi OTP quá nhanh. Vui lòng thử lại sau ít phút.");

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register, "register-otp");
        await _cacheService.SetAsync($"otp-sent:{email}", true, TimeSpan.FromMinutes(1));

        return true;
    }

    private async Task<bool> SendForgotPasswordOtpRequestAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        if (user == null)
            throw ErrorHelper.NotFound("Tài khoản không tồn tại.");

        if (user.Status == UserStatus.Suspended)
            throw ErrorHelper.Forbidden("Tài khoản đã bị cấm.");

        if (!user.IsEmailVerified)
            throw ErrorHelper.Conflict("Email chưa được xác minh. Không thể gửi OTP quên mật khẩu.");

        var counterKey = $"forgot-otp-count:{email}";
        var countValue = await _cacheService.GetAsync<int?>(counterKey) ?? 0;

        if (countValue >= 3)
            throw ErrorHelper.BadRequest("Bạn đã gửi quá nhiều OTP. Vui lòng thử lại sau 15 phút.");

        // Gửi OTP
        await GenerateAndSendOtpAsync(user, OtpPurpose.ForgotPassword, "forgot-otp");

        // Tăng số lần gửi và set timeout nếu là lần đầu tiên
        await _cacheService.SetAsync(counterKey, countValue + 1, TimeSpan.FromMinutes(15));

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
            Status = user.Status,
            PhoneNumber = user.Phone,
            RoleName = user.RoleName,
            CreatedAt = user.CreatedAt
        };
    }
}