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

public class AuthService : IAuthService
{
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;


    public AuthService(ILoggerService loggerService, IUnitOfWork unitOfWork, ICacheService cacheService,
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
            // Phase 1: Validate dữ liệu cơ bản
            if (string.IsNullOrWhiteSpace(registrationDto.Email) || !registrationDto.Email.Contains('@'))
            {
                _loggerService.Error("Invalid email format.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(registrationDto.Password) || registrationDto.Password.Length < 6)
            {
                _loggerService.Error("Password too short.");
                return null;
            }


            // Phase 2: Check cache hoặc DB xem user đã tồn tại
            var cacheKey = $"user:{registrationDto.Email}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
            if (cachedUser != null)
            {
                _loggerService.Info("Email already registered in cache.");
                return null;
            }

            var existingUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == registrationDto.Email);

            if (existingUser != null)
            {
                _loggerService.Info("Email already exists in database.");
                return null;
            }

            // Phase 3: Hash mật khẩu
            var passwordHasher = new PasswordHasher();
            var hashedPassword = passwordHasher.HashPassword(registrationDto.Password);

            // Phase 4: Tạo và lưu User
            var user = new User
            {
                Email = registrationDto.Email,
                Password = hashedPassword,
                FullName = registrationDto.FullName,
                Phone = registrationDto.PhoneNumber,
                DateOfBirth = registrationDto.DateOfBirth,
                AvatarUrl = registrationDto.AvatarUrl,
                Status = UserStatus.Pending,
                RoleName = RoleType.Customer,
                IsEmailVerified = false
            };
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Phase 5: Sinh OTP và lưu cache + DB
            var otpToken = OtpGenerator.GenerateToken(6, TimeSpan.FromMinutes(10));
            var otp = new OtpVerification
            {
                Target = user.Email,
                OtpCode = otpToken.Code,
                ExpiredAt = otpToken.ExpiresAtUtc,
                IsUsed = false,
                Purpose = OtpPurpose.Register
            };
            await _unitOfWork.OtpVerifications.AddAsync(otp);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"register-otp:{user.Email}", otpToken.Code, TimeSpan.FromMinutes(10));

            // Phase 6: Gửi email
            await _emailService.SendOtpVerificationEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                Otp = otpToken.Code,
                UserName = user.FullName
            });

            return new UserDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                DateOfBirth = user.DateOfBirth,
                PhoneNumber = user.Phone,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _loggerService.Error($"RegisterUserAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto, IConfiguration configuration)
    {
        _loggerService.Info("Login process initiated.");

        if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
            throw new Exception("400|Email hoặc mật khẩu không được để trống.");

        var cacheKey = $"user:{loginDto.Email}";
        var user = await _cacheService.GetAsync<User>(cacheKey);

        if (user == null)
        {
            user = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
                u.Email == loginDto.Email && !u.IsDeleted);

            if (user == null)
                throw new Exception("404|Tài khoản không tồn tại.");

            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
        }

        var passwordHasher = new PasswordHasher();
        if (!passwordHasher.VerifyPassword(loginDto.Password, user.Password))
            throw new Exception("401|Mật khẩu không chính xác.");

        if (user.Status == UserStatus.Pending)
            throw new Exception("403|Tài khoản chưa được kích hoạt.");

        _loggerService.Info($"User {loginDto.Email} authenticated. Generating tokens.");

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
        await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    public async Task<bool> VerifyEmailOtpAsync(string email, string otp)
    {
        try
        {
            // Phase 1: Validate cơ bản
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return false;

            // Phase 2: Lấy user
            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || user.IsEmailVerified)
                return false;

            // Phase 3: Ưu tiên kiểm tra OTP từ cache
            var cachedOtp = await _cacheService.GetAsync<string>($"register-otp:{email}");
            if (cachedOtp != null)
            {
                if (cachedOtp != otp) return false;
            }
            else
            {
                // Fallback: lấy từ DB nếu cache không có
                var otpRecord = await _unitOfWork.OtpVerifications.FirstOrDefaultAsync(o =>
                    o.Target == email && o.OtpCode == otp && o.Purpose == OtpPurpose.Register && !o.IsUsed);

                if (otpRecord == null || otpRecord.ExpiredAt < DateTime.UtcNow) return false;

                otpRecord.IsUsed = true;
                await _unitOfWork.OtpVerifications.Update(otpRecord);
            }

            // Phase 4: Cập nhật user
            user.IsEmailVerified = true;
            user.Status = UserStatus.Active;

            // Phase 5: Gửi email chúc mừng user
            await _emailService.SendRegistrationSuccessEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                UserName = user.FullName
            });

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            await _cacheService.RemoveAsync($"register-otp:{email}");
            await _cacheService.SetAsync($"user:{email}", user, TimeSpan.FromHours(1));

            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"VerifyEmailOtpAsync failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResendOtpAsync(string email) // dùng cho register
    {
        try
        {
            // Phase 1: Validate cơ bản
            if (string.IsNullOrWhiteSpace(email)) return false;

            // Phase 2: Kiểm tra user và trạng thái
            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || user.IsEmailVerified)
                return false;

            // Phase 3: Kiểm tra cache spam
            if (await _cacheService.ExistsAsync($"otp-sent:{email}"))
                return false;

            // Phase 4: Sinh OTP mới
            var otpToken = OtpGenerator.GenerateToken(6, TimeSpan.FromMinutes(10));
            var otp = new OtpVerification
            {
                Target = user.Email,
                OtpCode = otpToken.Code,
                ExpiredAt = otpToken.ExpiresAtUtc,
                IsUsed = false,
                Purpose = OtpPurpose.Register
            };

            await _unitOfWork.OtpVerifications.AddAsync(otp);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"register-otp:{email}", otpToken.Code, TimeSpan.FromMinutes(10));
            await _cacheService.SetAsync($"otp-sent:{email}", true, TimeSpan.FromMinutes(1)); // cooldown 1 phút

            // Phase 5: Gửi email
            await _emailService.SendOtpVerificationEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                Otp = otpToken.Code,
                UserName = user.FullName
            });

            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"ResendOtpAsync failed: {ex.Message}");
            return false;
        }
    }


    public async Task<bool> SendForgotPasswordOtpRequestAsync(string email) // có thể resend otp sau 1 phút cooldown 
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null || !user.IsEmailVerified)
                return false;

            // Cái này để chống spam gửi OTP
            if (await _cacheService.ExistsAsync($"forgot-otp-sent:{email}"))
                return false;

            var otpToken = OtpGenerator.GenerateToken(6, TimeSpan.FromMinutes(10));
            var otp = new OtpVerification
            {
                Target = user.Email,
                OtpCode = otpToken.Code,
                ExpiredAt = otpToken.ExpiresAtUtc,
                IsUsed = false,
                Purpose = OtpPurpose.ForgotPassword
            };

            await _unitOfWork.OtpVerifications.AddAsync(otp);
            await _unitOfWork.SaveChangesAsync();
            await _cacheService.SetAsync($"forgot-otp:{email}", otpToken.Code, TimeSpan.FromMinutes(10));
            await _cacheService.SetAsync($"forgot-otp-sent:{email}", true, TimeSpan.FromMinutes(1)); // cooldown 1p, sau 1p có thể resend otp

            await _emailService.SendForgotPasswordOtpEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                Otp = otpToken.Code,
                UserName = user.FullName
            });

            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"SendForgotPasswordOtpAsync failed: {ex.Message}");
            return false;
        }
    }


    public async Task<bool> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null || !user.IsEmailVerified)
                return false;

            // Ưu tiên kiểm tra OTP từ cache
            var cachedOtp = await _cacheService.GetAsync<string>($"forgot-otp:{email}");
            if (cachedOtp != null)
            {
                if (cachedOtp != otp) return false;
            }
            else
            {
                var otpRecord = await _unitOfWork.OtpVerifications.FirstOrDefaultAsync(o =>
                    o.Target == email && o.OtpCode == otp && o.Purpose == OtpPurpose.ForgotPassword && !o.IsUsed);

                if (otpRecord == null || otpRecord.ExpiredAt < DateTime.UtcNow) return false;

                otpRecord.IsUsed = true;
                await _unitOfWork.OtpVerifications.Update(otpRecord);
            }

            // Đặt lại mật khẩu
            var passwordHasher = new PasswordHasher();
            user.Password = passwordHasher.HashPassword(newPassword);

            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            await _cacheService.RemoveAsync($"forgot-otp:{email}");
            await _cacheService.SetAsync($"user:{email}", user, TimeSpan.FromHours(1));

            await _emailService.SendPasswordChangeEmailAsync(new EmailRequestDto
            {
                To = user.Email,
                UserName = user.FullName
            });

            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"ResetPasswordAsync failed: {ex.Message}");
            return false;
        }
    }
}