using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AccountDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class AccountService : IAccountService
{
    private readonly ICacheService _cacheService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;

    public AccountService(ILoggerService loggerService, IUnitOfWork unitOfWork, ICacheService cacheService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<bool> RegisterUserAsync(UserRegistrationDto registrationDto)
    {
        try
        {
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

            var cacheKey = $"user:{registrationDto.Email}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);

            if (cachedUser != null)
            {
                _loggerService.Info($"User {registrationDto.Email} is already registered (cached).");
                return false;
            }

            try
            {
                var passwordHasher = new PasswordHasher();
                passwordHasher.HashPassword(registrationDto.Password);
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Error hashing password: {ex.Message}");
                return false;
            }

            var user = new User
            {
                Email = registrationDto.Email,
                Password = registrationDto.Password,
                FullName = registrationDto.FullName
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(24));

            _loggerService.Info($"User {registrationDto.Email} registered successfully.");

            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"An error occurred while registering the user: {ex.Message}");
            return false;
        }
    }
}