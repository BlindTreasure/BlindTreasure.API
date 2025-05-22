using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Domain.Pagination;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/users")]
[ApiController]
[Authorize(Roles = "Admin")]
public class UserController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly IUserService _userService;

    public UserController(IUserService userService, IClaimsService claimsService)
    {
        _userService = userService;
        _claimsService = claimsService;
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
    [ProducesResponseType(typeof(ApiResult<Pagination<UserDto>>), 200)]
    public async Task<IActionResult> GetAllUsers([FromQuery] UserQueryParameter param)
    {
        try
        {
            var result = await _userService.GetAllUsersAsync(param);

            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách user thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Tạo user mới (admin có thể set role bất kỳ).
    /// </summary>
    // [Authorize]
    [HttpPost("users")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 400)]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
    {
        try
        {
            var result = await _userService.CreateUserAsync(dto);
            if (result == null)
                return BadRequest(ApiResult<UserDto>.Failure("400", "Email đã tồn tại hoặc dữ liệu không hợp lệ."));

            return Ok(ApiResult<UserDto>.Success(result, "200", "Tạo user thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Cập nhật trạng thái user (ban, deactive, active lại, ...).
    /// </summary>
    [HttpPut("users/{userId}/status")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UserStatus status)
    {
        try
        {
            var result = await _userService.UpdateUserStatusAsync(userId, status);
            if (result == null)
                return NotFound(ApiResult<UserDto>.Failure("404", "Không tìm thấy user."));
            return Ok(ApiResult<UserDto>.Success(result, "200", "Cập nhật trạng thái user thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy thông tin user theo id.
    /// </summary>
    [HttpGet("users/{userId}")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        try
        {
            var result = await _userService.GetUserDetailsByIdAsync(userId);
            if (result == null)
                return NotFound(ApiResult<UserDto>.Failure("404", "Không tìm thấy user."));

            return Ok(ApiResult<UserDto>.Success(result, "200", "Lấy thông tin user thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}