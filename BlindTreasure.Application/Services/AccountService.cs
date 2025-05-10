using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AccountDTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class AccountService : IAccountService
{
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;


    public AccountService(ILoggerService loggerService, IUnitOfWork unitOfWork, ICacheService cacheService,
        IEmailService emailService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
    }

    public async Task<bool> RegisterUserAsync(UserRegistrationDto registrationDto)
    {
        try
        {
            // 1. Validate đầu vào
            if (string.IsNullOrWhiteSpace(registrationDto.Password))
            {
                _loggerService.Error("Password cannot be empty.");
                return false;
            }

            if (registrationDto.Password.Length < 6)
            {
                _loggerService.Error("Password must be at least 6 characters long.");
                return false;
            }

            // 2. Kiểm tra cache tránh đăng ký trùng
            var cacheKey = $"user:{registrationDto.Email}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
            if (cachedUser != null)
            {
                _loggerService.Info($"User {registrationDto.Email} is already registered (cached).");
                return false;
            }

            // 3. Hash password
            try
            {
                var passwordHasher = new PasswordHasher();
                registrationDto.Password = passwordHasher.HashPassword(registrationDto.Password);
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Error hashing password: {ex.Message}");
                return false;
            }

            // 4. Tạo mới User, mặc định chưa xác thực email
            var user = new User
            {
                Email = registrationDto.Email,
                Password = registrationDto.Password,
                FullName = registrationDto.FullName,
                Status = "ACTIVE",
                RoleId = Guid.Parse("..."), // gán RoleId phù hợp
                IsEmailVerified = false
            };

            // 5. Lưu user vào DB
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // 6. Sinh OTP và gán vào user
            var otpToken = OtpGenerator.GenerateToken(6, TimeSpan.FromMinutes(10));
            user.EmailVerifyToken = otpToken.Code;
            user.EmailVerifyTokenExpires = otpToken.ExpiresAtUtc;

            // 7. Cập nhật lại user với token
            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            // 8. Cập nhật cache
            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(24));

            // 9. Gửi email xác thực OTP
            var emailRequest = new EmailRequestDto
            {
                To = registrationDto.Email,
                Otp = otpToken.Code,
                UserName = registrationDto.FullName
            };
            await _emailService.SendOtpVerificationEmailAsync(emailRequest);

            _loggerService.Info($"OTP verification email sent to {registrationDto.Email}.");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"An error occurred while registering the user: {ex.Message}");
            return false;
        }
    }
}