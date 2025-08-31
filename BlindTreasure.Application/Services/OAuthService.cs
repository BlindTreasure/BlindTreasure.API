using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace BlindTreasure.Application.Services;

public class OAuthService : IOAuthService
{
    public readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminService _userService;
    private readonly string clientId;
    private readonly string passwordCharacters;

    public OAuthService(IAdminService userService, IUnitOfWork unitOfWork, IConfiguration configuration,
        IAuthService authService)
    {
        _userService = userService;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        clientId = _configuration["OAuthSettings:ClientId"] ??
                   throw new Exception("Missing google client Id in config");
        passwordCharacters = _configuration["OAuthSettings:PasswordCharacters"] ??
                             throw new Exception("Missing google oauth setting in config");
        _authService = authService;
    }


    public async Task<LoginResponseDto> AuthenticateWithGoogle(string token)
    {
        if (string.IsNullOrWhiteSpace(clientId)) throw ErrorHelper.BadRequest("Client Id is missing");

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new List<string> { clientId }
        };

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);
        }
        catch
        {
            throw ErrorHelper.Forbidden("Invalid token");
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
            throw ErrorHelper.BadRequest("Null Payload từ google");

        try
        {
            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == payload.Email && !u.IsDeleted);
            if (user == null) throw ErrorHelper.BadRequest("Account not found");
            var result = await _authService.LoginAsync(new LoginRequestDto
            {
                Email = user.Email,
                Password = "", // Mật khẩu tạm thời, sẽ được thay đổi sau khi đăng nhập
                IsLoginGoole = true // Không cần nhớ đăng nhập cho OAuth
            }, _configuration);

            return result;
        }
        catch (Exception ex)
        {
            // User not found, register a new one
            if (ex.Message.Contains("Account not found", StringComparison.OrdinalIgnoreCase))
            {
                var hashedPassword = new PasswordHasher().HashPassword("123456");


                var request = new UserCreateDto
                {
                    Email = payload.Email,
                    FullName = payload.Name,
                    Password = "123456",
                    AvatarUrl = payload.Picture,
                    RoleName = RoleType.Customer,
                    PhoneNumber = "" // Nếu không có số điện thoại, có thể để trống
                };

                var user = await _userService.CreateUserAsync(request);
                if (user == null)
                    throw ErrorHelper.Internal("Failed to create user");

                var result = await _authService.LoginAsync(new LoginRequestDto
                {
                    Email = user.Email,
                    Password = "123456", // Mật khẩu tạm thời, sẽ được thay đổi sau khi đăng nhập
                    IsLoginGoole = true // Không cần nhớ đăng nhập cho OAuth
                }, _configuration);

                return result;
            }

            // Nếu là lỗi khác, quăng lại để middleware xử lý
            throw;
        }
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