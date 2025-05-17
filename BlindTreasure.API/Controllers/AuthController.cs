using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IClaimsService _claimsService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IClaimsService claimsService, IConfiguration configuration)
    {
        _authService = authService;
        _claimsService = claimsService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 409)]
    public async Task<IActionResult> Register([FromBody] UserRegistrationDto dto)
    {
        try
        {
            var result = await _authService.RegisterUserAsync(dto);
            return Ok(ApiResult<UserDto>.Success(result!, "200", "Đăng ký thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 400)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto, _configuration);
            return Ok(ApiResult<LoginResponseDto>.Success(result!, "200", "Đăng nhập thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<LoginResponseDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = _claimsService.GetCurrentUserId;
            var result = await _authService.LogoutAsync(userId);
            return Ok(ApiResult<object>.Success(result!, "200", "Đăng xuất thành công. "));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
    {
        var verified = await _authService.VerifyEmailOtpAsync(dto.Email, dto.Otp);
        if (!verified)
            return BadRequest(ApiResult.Failure("400", "OTP không hợp lệ hoặc đã hết hạn."));

        return Ok(ApiResult.Success("200", "Xác thực thành công. Tài khoản đã được kích hoạt."));
    }

    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto dto)
    {
        var sent = await _authService.ResendRegisterOtpAsync(dto.Email);
        if (!sent)
            return BadRequest(ApiResult.Failure("400",
                "Không thể gửi lại OTP. Email không tồn tại hoặc đã được xác thực."));

        return Ok(ApiResult.Success("200", "OTP đã được gửi lại thành công."));
    }


    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
    {
        var sent = await _authService.SendForgotPasswordOtpRequestAsync(dto.Email);
        if (!sent)
            return BadRequest(ApiResult.Failure("400", "Không thể gửi OTP. Email không hợp lệ hoặc chưa xác thực."));
        return Ok(ApiResult.Success("200", "OTP đã được gửi đến email của bạn."));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var reset = await _authService.ResetPasswordAsync(dto.Email, dto.Otp, dto.NewPassword);
        if (!reset)
            return BadRequest(ApiResult.Failure("400", "OTP không hợp lệ, đã hết hạn hoặc dữ liệu không hợp lệ."));
        return Ok(ApiResult.Success("200", "Mật khẩu đã được đặt lại thành công."));
    }
}