using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AddressDTOs;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/me")]
[ApiController]
public class PersonalController : ControllerBase
{
    private readonly IAddressService _addressService;
    private readonly IClaimsService _claimsService;
    private readonly ISellerService _sellerService;
    private readonly IUserService _userService;

    public PersonalController(IClaimsService claimsService, IUserService userService, ISellerService sellerService,
        IAddressService addressService)
    {
        _claimsService = claimsService;
        _userService = userService;
        _sellerService = sellerService;
        _addressService = addressService;
    }

    /// <summary>
    ///     Lấy thông tin Seller theo userId login hiện tại.
    /// </summary>
    [Authorize]
    [HttpGet("seller-profile")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    public async Task<IActionResult> GetSellerDetails()
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var data = await _sellerService.GetSellerProfileByUserIdAsync(userId);
            return Ok(ApiResult<object>.Success(data, "200", "Lấy thông tin của Seller thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, error);
        }
    }


    /// <summary>
    ///     Lấy thông tin profile cá nhân user theo id đang login.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
    public async Task<IActionResult> GetUserProfile()
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
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
    [HttpPut]
    [ProducesResponseType(typeof(ApiResult<UpdateProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
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


    /// <summary>
    ///     Customer cập nhật thông tin cá nhân.
    /// </summary>
    [Authorize]
    [HttpPut("avatar")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateAvatar(IFormFile file)
    {
        var userId = _claimsService.CurrentUserId;

        if (file.Length == 0)
            return BadRequest(ApiResult.Failure("400", "File không hợp lệ."));

        var result = await _userService.UploadAvatarAsync(userId, file);
        if (result == null)
            return BadRequest(ApiResult.Failure("400", "Không thể cập nhật avatar."));

        return Ok(ApiResult<UpdateAvatarResultDto>.Success(result, "200", "Cập nhật avatar thành công."));
    }

    /// <summary>
    ///     Seller cập nhật thông tin cá nhân và doanh nghiệp của mình.
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpPut("seller-profile")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateSellerProfile([FromBody] UpdateSellerInfoDto dto)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var result = await _sellerService.UpdateSellerInfoAsync(userId, dto);
            return Ok(ApiResult<object>.Success(result, "200", "Cập nhật hồ sơ Seller thành công."));
        }
        catch (Exception ex)
        {
            var status = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(status, error);
        }
    }

    /// <summary>
    ///     Lấy danh sách địa chỉ của user hiện tại.
    /// </summary>
    [Authorize]
    [HttpGet("addresses")]
    [ProducesResponseType(typeof(ApiResult<List<AddressDto>>), 200)]
    public async Task<IActionResult> GetAddresses()
    {
        try
        {
            var result = await _addressService.GetCurrentUserAddressesAsync();
            return Ok(ApiResult<List<AddressDto>>.Success(result, "200", "Lấy danh sách địa chỉ thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<AddressDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Lấy chi tiết một địa chỉ theo id.
    /// </summary>
    [Authorize]
    [HttpGet("addresses/{id}")]
    [ProducesResponseType(typeof(ApiResult<AddressDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAddressById(Guid id)
    {
        try
        {
            var result = await _addressService.GetByIdAsync(id);
            return Ok(ApiResult<AddressDto>.Success(result, "200", "Lấy địa chỉ thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<AddressDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Thêm mới địa chỉ cho user hiện tại. Không cần truyền field IsDefault hoặc muốn có thể tự truyền true tùy vào mục
    ///     đích
    /// </summary>
    [Authorize]
    [HttpPost("addresses")]
    [ProducesResponseType(typeof(ApiResult<AddressDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressDto dto)
    {
        try
        {
            var result = await _addressService.CreateAsync(dto);
            return Ok(ApiResult<AddressDto>.Success(result, "200", "Thêm địa chỉ thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<AddressDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Cập nhật địa chỉ theo id.
    /// </summary>
    [Authorize]
    [HttpPut("addresses/{id}")]
    [ProducesResponseType(typeof(ApiResult<AddressDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressDto dto)
    {
        try
        {
            var result = await _addressService.UpdateAsync(id, dto);
            return Ok(ApiResult<AddressDto>.Success(result, "200", "Cập nhật địa chỉ thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<AddressDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Xóa địa chỉ theo id.
    /// </summary>
    [Authorize]
    [HttpDelete("addresses/{id}")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        try
        {
            var result = await _addressService.DeleteAsync(id);
            if (result)
                return Ok(ApiResult<object>.Success(null, "200", "Xóa địa chỉ thành công."));
            return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy địa chỉ."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Đặt một địa chỉ là mặc định.
    /// </summary>
    [Authorize]
    [HttpPut("addresses/{id}/default")]
    [ProducesResponseType(typeof(ApiResult<AddressDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> SetDefaultAddress(Guid id)
    {
        try
        {
            var result = await _addressService.SetDefaultAsync(id);
            return Ok(ApiResult<AddressDto>.Success(result, "200", "Cập nhật địa chỉ mặc định thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<AddressDto>(ex);
            return StatusCode(statusCode, error);
        }
    }
}