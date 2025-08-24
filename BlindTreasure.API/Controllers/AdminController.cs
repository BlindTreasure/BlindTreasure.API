using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

//[Authorize(Roles = "Admin,Staff")]
[ApiController]
[Route("api/administrator")] // hoặc "api/seller-verification"
public class AdminController : ControllerBase
{
    private readonly ISellerVerificationService _sellerVerificationService;
    private readonly IClaimsService _claimsService;
    private readonly IUserService _userService;
    private readonly IOrderService _orderService;
    private readonly IPayoutService _payoutService;

    public AdminController(ISellerVerificationService sellerVerificationService, IClaimsService claimsService, IUserService userService, IOrderService orderService, IPayoutService payoutService)
    {
        _sellerVerificationService = sellerVerificationService;
        _claimsService = claimsService;
        _userService = userService;
        _orderService = orderService;
        _payoutService = payoutService;
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
    /// </summary>
    [HttpPut("users/{userId}/status")]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UpdateUserStatusDto dto)
    {
        try
        {
            var result = await _userService.UpdateUserStatusAsync(userId, dto.Status, dto.Reason);
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

    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResult<Pagination<OrderDto>>), 200)]
    public async Task<IActionResult> GetAllOrders([FromQuery] OrderAdminQueryParameter param)
    {
        try
        {
            var result = await _orderService.GetAllOrdersForAdminAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpGet("payouts")]
    [Authorize]
    public async Task<IActionResult> GetPayoutsForAdmin([FromQuery] PayoutAdminQueryParameter param)
    {
        try
        {
            var result = await _payoutService.GetPayoutsForAdminAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.Count,
                pageSize = param.PageSize,
                currentPage = param.PageIndex,
                totalPages = (int)Math.Ceiling((double)result.Count / param.PageSize)
            }, "200", "Lấy danh sách payouts thành công."));
        }
        catch (Exception ex)
        {
            //_.Error($"[GetPayoutsForAdmin] {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure("500", "Có lỗi xảy ra khi lấy danh sách payouts."));
        }
    }


}