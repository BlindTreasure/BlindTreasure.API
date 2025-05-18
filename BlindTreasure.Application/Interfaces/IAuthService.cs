using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace BlindTreasure.Application.Interfaces;

public interface IAuthService
{
    Task<UserDto?> RegisterUserAsync(UserRegistrationDto registrationDto);
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto, IConfiguration configuration);
    Task<bool> LogoutAsync(Guid userId);

    Task<LoginResponseDto?> RefreshTokenAsync(TokenRefreshRequestDto refreshTokenDto,
        IConfiguration configuration);

    //OTP & emails
    Task<bool> ResendOtpAsync(string email, OtpType type);
    Task<bool> VerifyEmailOtpAsync(string email, string otp);
    Task<bool> ResetPasswordAsync(string email, string otp, string newPassword);
}