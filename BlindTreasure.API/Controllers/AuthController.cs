using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BlindTreasure.API.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        this.configuration = configuration;
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
        //IConfiguration configuration = new ConfigurationBuilder()
        //    .SetBasePath(Directory.GetCurrentDirectory())
        //    .AddEnvironmentVariables()
        //    .Build();
        try
        {
            var result = await _authService.LoginAsync(dto, configuration);
            return Ok(ApiResult<LoginResponseDto>.Success(result!, "200", "Đăng nhập thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<LoginResponseDto>(ex);
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

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        // Lấy userId từ JWT claims: ưu tiên "sub", fallback sang NameIdentifier
        // Thật ra là nên xài claim service nhưng code thế này cũng tiện, đỡ inject tùm lum
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResult.Failure("401", "Không xác định được người dùng."));

        var result = await _authService.UpdateProfileAsync(userId, dto);
        if (result == null)
            return BadRequest(ApiResult.Failure("400", "Không thể cập nhật thông tin."));
        return Ok(ApiResult<UserDto>.Success(result, "200", "Cập nhật thông tin thành công."));
    }

    [Authorize]
    [HttpPost("profile/avatar")]
    [ProducesResponseType(typeof(ApiResult<UpdateAvatarResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateAvatar([FromForm] IFormFile file)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResult.Failure("401", "Không xác định được người dùng."));

        if (file == null || file.Length == 0)
            return BadRequest(ApiResult.Failure("400", "File không hợp lệ."));

        var result = await _authService.UpdateAvatarAsync(userId, file);
        if (result == null)
            return BadRequest(ApiResult.Failure("400", "Không thể cập nhật avatar."));

        return Ok(ApiResult<UpdateAvatarResultDto>.Success(result, "200", "Cập nhật avatar thành công."));
    }
}