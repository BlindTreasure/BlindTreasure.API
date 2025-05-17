using BlindTreasure.Application.Interfaces;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/customer")]
[ApiController]
public class CustomerController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly IUserService _userService;

    public CustomerController(IClaimsService claimsService, IUserService userService)
    {
        _claimsService = claimsService;
        _userService = userService;
    }

    //[HttpGet("profile")]
    //[Authorize(Policy = "CustomerPolicy")]
    //[ProducesResponseType(typeof(ApiResult<CurrentUserDto>), 200)]
    //[ProducesResponseType(typeof(ApiResult<CurrentUserDto>), 400)]
    //public async Task<IActionResult> GetCustomerProfile()
    //{
    //    try
    //    {
    //        var currentUserId = _claimsService.GetCurrentUserId;

    //        var result = await _userService.GetUserDetails(currentUserId);

    //        return Ok(ApiResult<CurrentUserDto>.Success(result, "200", "Lấy thông tin người dùng thành công."));
    //    }
    //    catch (Exception ex)
    //    {
    //        var statusCode = ExceptionUtils.ExtractStatusCode(ex.Message);
    //        var message = ex.Message.Contains('|') ? ex.Message.Split('|', 2)[1] : "Lỗi không xác định.";

    //        return StatusCode(statusCode, ApiResult<CurrentUserDto>.Failure(statusCode.ToString(), message));
    //    }
    //}
}