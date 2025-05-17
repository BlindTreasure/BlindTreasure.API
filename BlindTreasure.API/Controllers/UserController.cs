using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    /// <summary>
    /// API cho admin quản lý user (CRUD, update avatar, ...).
    /// </summary>
    [Route("api/users")]
    [ApiController]
  //  [Authorize(Roles = "Admin")]

    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }




        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<Pagination<UserDto>>), 200)]
        public async Task<IActionResult> GetAllUsers([FromQuery] PaginationParameter param)
        {
            var result = await _userService.GetAllUsersAsync(param);
            return Ok(ApiResult<object>
                .Success(new
                {
                    result,
                    count = result.TotalCount,
                    pageSize = result.PageSize,
                    currentPage = result.CurrentPage,
                    totalPages = result.TotalPages,
                }, "200", "Lấy danh sách user thành công."));
            //return Ok(ApiResult<Pagination<UserDto>>.Success(result.TotalCount, "200", "Lấy danh sách user thành công."));
        }

        /// <summary>
        /// Lấy thông tin user theo id.
        /// </summary>
        [HttpGet("{userId}")]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var result = await _userService.GetUserByIdAsync(userId);
            if (result == null)
                return NotFound(ApiResult<UserDto>.Failure("404", "Không tìm thấy user."));
            return Ok(ApiResult<UserDto>.Success(result, "200", "Lấy thông tin user thành công."));
        }

        /// <summary>
        /// Tạo user mới (admin có thể set role bất kỳ).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 400)]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
        {
            var result = await _userService.CreateUserAsync(dto);
            if (result == null)
                return BadRequest(ApiResult<UserDto>.Failure("400", "Email đã tồn tại hoặc dữ liệu không hợp lệ."));
            return Ok(ApiResult<UserDto>.Success(result, "200", "Tạo user thành công."));
        }

        /// <summary>
        /// Cập nhật thông tin user (trừ avatar).
        /// </summary>
        [HttpPut("{userId}")]
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
        /// Cập nhật avatar cho user (admin thao tác).
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

        /// <summary>
        /// Xóa (deactivate) user.
        /// </summary>
        [HttpDelete("{userId}")]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            var success = await _userService.DeleteUserAsync(userId);
            if (!success)
                return NotFound(ApiResult.Failure("404", "Không tìm thấy user."));
            return Ok(ApiResult.Success("200", "Xóa user thành công."));
        }
    }
}
