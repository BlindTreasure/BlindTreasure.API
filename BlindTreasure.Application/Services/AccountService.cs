using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AccountDTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
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

    public async Task<UserDto> RegisterUserAsync(UserRegistrationDto registrationDto)
    {
        try
        {
            // Phase 1: Validate the input data
            if (string.IsNullOrWhiteSpace(registrationDto.Password))
            {
                _loggerService.Error("Password cannot be empty.");
                return null; // Return null to indicate failure
            }

            if (registrationDto.Password.Length < 6)
            {
                _loggerService.Error("Password must be at least 6 characters long.");
                return null;
            }

            // Phase 2: Check if the user is already registered (cached)
            var cacheKey = $"user:{registrationDto.Email}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
            if (cachedUser != null)
            {
                _loggerService.Info($"User {registrationDto.Email} is already registered (cached).");
                return null;
            }

            // Phase 3: Hash the password
            try
            {
                var passwordHasher = new PasswordHasher();
                registrationDto.Password = passwordHasher.HashPassword(registrationDto.Password);
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Error hashing password: {ex.Message}");
                return null;
            }

            // Phase 4: Create the new User entity
            var user = new User
            {
                Email = registrationDto.Email,
                Password = registrationDto.Password,
                FullName = registrationDto.FullName,
                Status = UserStatus.Active,
                RoleName = RoleType.Customer,
                IsEmailVerified = false,
            };

            // Phase 5: Save the user to the database
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Phase 6: Generate OTP for email verification
            var otpToken = OtpGenerator.GenerateToken(6, TimeSpan.FromMinutes(10));
            user.EmailVerifyToken = otpToken.Code;
            user.EmailVerifyTokenExpires = otpToken.ExpiresAtUtc;

            // Phase 7: Update user with OTP token
            await _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            // Phase 8: Update cache with new user data
            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(24));

            // Phase 9: Send OTP verification email
            var emailRequest = new EmailRequestDto
            {
                To = registrationDto.Email,
                Otp = otpToken.Code,
                UserName = registrationDto.FullName
            };
            await _emailService.SendOtpVerificationEmailAsync(emailRequest);

            _loggerService.Info($"OTP verification email sent to {registrationDto.Email}.");

            // Return the UserDto for the successfully registered user
            return new UserDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.Phone,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _loggerService.Error($"An error occurred while registering the user: {ex.Message}");
            return null; // Return null if any error occurs
        }
    }
}