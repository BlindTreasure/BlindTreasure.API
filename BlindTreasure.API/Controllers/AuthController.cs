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
    private readonly IOAuthService _oAuthService;
    private readonly string passwordCharacters;


    public AuthController(IAuthService authService, IClaimsService claimsService, IConfiguration configuration,
        IOAuthService oAuthService)
    {
        _authService = authService;
        _claimsService = claimsService;
        _configuration = configuration;
        _oAuthService = oAuthService;
        passwordCharacters = _configuration["OAuthSettings:PasswordCharacters"] ??
                             throw new Exception("Missing google oauth setting in config");
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 409)]
    public async Task<IActionResult> RegisterCustomer([FromBody] UserRegistrationDto dto)
    {
        try
        {
            var result = await _authService.RegisterCustomerAsync(dto);
            return Ok(ApiResult<UserDto>.Success(result!, "200", "Đăng ký tài khoản khách hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPost("register-seller")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 409)]
    public async Task<IActionResult> RegisterSeller([FromBody] SellerRegistrationDto dto)
    {
        try
        {
            var result = await _authService.RegisterSellerAsync(dto);
            return Ok(ApiResult<UserDto>.Success(result!, "200", "Đăng ký tài khoản người bán thành công."));
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
            dto.IsLoginGoole = false; // Đặt mặc định là không đăng nhập bằng Google
            var result = await _authService.LoginAsync(dto, _configuration);
            return Ok(ApiResult<LoginResponseDto>.Success(result!, "200",
                "Đăng nhập thành công. Chào mừng bạn trở lại!"));
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
            var userId = _claimsService.CurrentUserId;
            var result = await _authService.LogoutAsync(userId);
            return Ok(ApiResult<object>.Success(result!, "200", "Đăng xuất thành công. Hẹn gặp lại bạn!"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> RefreshToken([FromBody] TokenRefreshRequestDto requestToken)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(requestToken, _configuration);
            return Ok(ApiResult<object>.Success(result!, "200", "Làm mới phiên đăng nhập thành công."));
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
            return BadRequest(ApiResult.Failure("400", "Mã OTP không hợp lệ hoặc đã hết hạn. Vui lòng thử lại."));

        return Ok(ApiResult.Success("200", "Xác thực thành công. Tài khoản của bạn đã được kích hoạt."));
    }

    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> ResendOtp([FromForm] ResendOtpRequestDto dto)
    {
        try
        {
            var sent = await _authService.ResendOtpAsync(dto.Email, dto.Type);
            return Ok(ApiResult<object>.Success(sent!, "200",
                "Mã OTP đã được gửi lại thành công. Vui lòng kiểm tra email của bạn."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var reset = await _authService.ResetPasswordAsync(dto.Email, dto.Otp, dto.NewPassword);
        if (!reset)
            return BadRequest(ApiResult.Failure("400",
                "Mã OTP không hợp lệ, đã hết hạn hoặc thông tin đặt lại mật khẩu không chính xác."));
        return Ok(ApiResult.Success("200", "Mật khẩu của bạn đã được đặt lại thành công."));
    }

    /// <summary>
    ///     Đăng nhập bằng Google OAuth2.
    /// </summary>
    [HttpPost("login-google")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 400)]
    public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleLoginRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
                return BadRequest(ApiResult.Failure("400", "Token Google không hợp lệ. Vui lòng thử lại."));

            var result = await _oAuthService.AuthenticateWithGoogle(dto.Token);


            return Ok(ApiResult<LoginResponseDto>.Success(result!, "200",
                "Đăng nhập bằng Google thành công. Chào mừng bạn trở lại!"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}