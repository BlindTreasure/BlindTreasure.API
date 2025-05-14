using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using Microsoft.Extensions.Configuration;

namespace BlindTreasure.Application.Interfaces;

public interface IAuthService
{
    Task<UserDto?> RegisterUserAsync(UserRegistrationDto registrationDto);
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto, IConfiguration configuration);

    Task<bool> VerifyEmailOtpAsync(string email, string otp);

    Task<bool> ResendOtpAsync(string email);
    Task<bool> ResetPasswordAsync(string email, string otp, string newPassword);
    Task<bool> SendForgotPasswordOtpRequestAsync(string email);
}