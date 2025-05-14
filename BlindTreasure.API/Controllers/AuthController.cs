using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 400)]
    public async Task<IActionResult> Register([FromBody] UserRegistrationDto dto)
    {
        var result = await _authService.RegisterUserAsync(dto);
        if (result == null)
            return BadRequest(ApiResult<UserDto>.Failure("400", "Email đã tồn tại hoặc dữ liệu không hợp lệ."));

        return Ok(ApiResult<UserDto>.Success(result, "200",
            "Đăng ký thành công. Vui lòng kiểm tra email để xác thực."));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 400)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddEnvironmentVariables()
            .Build();
        try
        {
            var result = await _authService.LoginAsync(dto, configuration);
            return Ok(ApiResult<LoginResponseDto>.Success(result ?? throw new InvalidOperationException(), "200",
                "Đăng nhập thành công."));
        }
        catch (Exception ex)
        {
            // Sử dụng ExceptionUtils để trích xuất mã lỗi và message
            var statusCode = ExceptionUtils.ExtractStatusCode(ex.Message); // Lấy mã lỗi HTTP từ exception message
            var message =
                ex.Message.Contains('|') ? ex.Message.Split('|', 2)[1] : "Lỗi không xác định.";

            // Trả về response với mã lỗi và message từ ExceptionUtils
            return StatusCode(statusCode, ApiResult<LoginResponseDto>.Failure(statusCode.ToString(), message));
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
        var sent = await _authService.ResendOtpAsync(dto.Email);
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