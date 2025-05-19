using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    [Route("api")]
    [ApiController]
    public class PersonalController : ControllerBase
    {
        private readonly IClaimsService _claimsService;
        private readonly IUserService _userService;

        public PersonalController(IClaimsService claimsService, IUserService userService)
        {
            _claimsService = claimsService;
            _userService = userService;
        }

        /// <summary>
        ///     Lấy thông tin user theo id.
        /// </summary>
        [HttpGet("me")]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
        public async Task<IActionResult> GetUserProfile()
        {
            try
            {
                var userId = _claimsService.GetCurrentUserId;
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

        [Authorize]
        [HttpPut("me")]
        [ProducesResponseType(typeof(ApiResult<UpdateProfileDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userId = _claimsService.GetCurrentUserId;
                var result = await _userService.UpdateProfileAsync(userId, dto);
                if (result == null)
                    return BadRequest(ApiResult.Failure("400", "Không thể cập nhật thông tin."));

                return Ok(ApiResult<UserDto>.Success(result, "200", "Cập nhật thông tin thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<UserDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }

        [Authorize]
        [HttpPut("me/avatar")]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        public async Task<IActionResult> UpdateAvatar(IFormFile file)
        {
            var userId = _claimsService.GetCurrentUserId;

            if (file == null || file.Length == 0)
                return BadRequest(ApiResult.Failure("400", "File không hợp lệ."));

            var result = await _userService.UploadAvatarAsync(userId, file);
            if (result == null)
                return BadRequest(ApiResult.Failure("400", "Không thể cập nhật avatar."));

            return Ok(ApiResult<UpdateAvatarResultDto>.Success(result, "200", "Cập nhật avatar thành công."));
        }
    }
}
