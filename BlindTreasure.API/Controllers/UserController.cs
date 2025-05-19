using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/customer")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly IUserService _userService;

    public UserController(IClaimsService claimsService, IUserService userService)
    {
        _claimsService = claimsService;
        _userService = userService;
    }

    /// <summary>
    ///     Cập nhật thông tin user (trừ avatar).
    /// </summary>
    [HttpPut("users/{userId}")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UserUpdateDto dto)
    {
        var success = await _userService.UpdateUserAsync(userId, dto);
        if (!success)
            return NotFound(ApiResult.Failure("404", "Không tìm thấy user hoặc dữ liệu không hợp lệ."));
        return Ok(ApiResult.Success("200", "Cập nhật user thành công."));
    }

    /// <summary>
    ///     Cập nhật avatar cho user (admin thao tác).
    /// </summary>
    [HttpPost("{userId}/avatar")]
    [ProducesResponseType(typeof(ApiResult<UpdateAvatarResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateUserAvatar(Guid userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResult.Failure("400", "File không hợp lệ."));

        var result = await _userService.UpdateUserAvatarAsync(userId, file);
        if (result == null)
            return NotFound(ApiResult.Failure("404", "Không tìm thấy user hoặc upload thất bại."));
        return Ok(ApiResult<UpdateAvatarResultDto>.Success(result, "200", "Cập nhật avatar thành công."));
    }

    [Authorize]
    [HttpPost("profile/avatar")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateAvatar(IFormFile file)
    {
        var userId = _claimsService.GetCurrentUserId;

        if (file == null || file.Length == 0)
            return BadRequest(ApiResult.Failure("400", "File không hợp lệ."));

        var result = await _userService.UpdateAvatarAsync(userId, file);
        if (result == null)
            return BadRequest(ApiResult.Failure("400", "Không thể cập nhật avatar."));

        return Ok(ApiResult<UpdateAvatarResultDto>.Success(result, "200", "Cập nhật avatar thành công."));
    }

    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResult<UpdateProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        try
        {
            var userId = _claimsService.GetCurrentUserId;
            var result = await _userService.UpdateProfileAsync(userId, dto);
            return Ok(ApiResult<UserDto>.Success(result, "200", "Cập nhật thông tin thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}