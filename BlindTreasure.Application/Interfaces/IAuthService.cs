using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using Microsoft.AspNetCore.Http;
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
    Task<bool> VerifyEmailOtpAsync(string email, string otp);
    Task<bool> ResendRegisterOtpAsync(string email);
    Task<bool> ResetPasswordAsync(string email, string otp, string newPassword);
    Task<bool> SendForgotPasswordOtpRequestAsync(string email);
    Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
    Task<UpdateAvatarResultDto?> UpdateAvatarAsync(Guid userId, IFormFile file);
    Task<UserDto?> GetUserByIdWithCache(Guid userId);
}