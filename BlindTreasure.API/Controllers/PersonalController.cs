using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AddressDTOs;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.PromotionDTOs;
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
    private readonly IPromotionService _promotionService;

    public PersonalController(IClaimsService claimsService, IUserService userService, ISellerService sellerService,
        IAddressService addressService, IPromotionService promotionService)
    {
        _claimsService = claimsService;
        _userService = userService;
        _sellerService = sellerService;
        _addressService = addressService;
        _promotionService = promotionService;
    }

    /// <summary>
    /// Lấy thông tin tổng quan của Seller đang đăng nhập.
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("seller-overview")]
    [ProducesResponseType(typeof(ApiResult<SellerOverviewDto>), 200)]
    public async Task<IActionResult> GetMySellerOverview()
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var seller = await _sellerService.GetSellerProfileByUserIdAsync(userId);
            if (seller == null)
                return NotFound(ApiResult<SellerOverviewDto>.Failure("404", "Không tìm thấy seller."));

            var overview = await _sellerService.GetSellerOverviewAsync(seller.SellerId);
            return Ok(ApiResult<SellerOverviewDto>.Success(overview, "200", "Lấy thông tin tổng quan seller thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<SellerOverviewDto>(ex);
            return StatusCode(statusCode, error);
        }
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
                return NotFound(ApiResult<UserDto>.Failure("404", "Không tìm thấy người dùng."));

            return Ok(ApiResult<UserDto>.Success(result, "200", "Lấy thông tin người dùng thành công."));
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
            return BadRequest(ApiResult.Failure("400", "Tập tin không hợp lệ."));

        var result = await _userService.UploadAvatarAsync(userId, file);
        if (result == null)
            return BadRequest(ApiResult.Failure("400", "Không thể cập nhật ảnh đại diện."));

        return Ok(ApiResult<UpdateAvatarResultDto>.Success(result, "200", "Cập nhật ảnh đại diện thành công."));
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
            return Ok(ApiResult<object>.Success(result, "200", "Cập nhật hồ sơ người bán thành công."));
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
            var result = await _addressService.GetAddressByIdAsync(id);
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
            var result = await _addressService.CreateAddressAsync(dto);
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
            var result = await _addressService.UpdateAddressAsync(id, dto);
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
            var result = await _addressService.DeleteAddressAsync(id);
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
            var result = await _addressService.SetDefaultAddressAsync(id);
            return Ok(ApiResult<AddressDto>.Success(result, "200", "Cập nhật địa chỉ mặc định thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<AddressDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Seller cập nhật avatar riêng.
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpPut("seller-avatar")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> UpdateSellerAvatar(IFormFile file)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var avatarUrl = await _sellerService.UpdateSellerAvatarAsync(userId, file);
            return Ok(ApiResult<string>.Success(avatarUrl, "200", "Cập nhật ảnh đại diện người bán thành công."));
        }
        catch (Exception ex)
        {
            var status = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(status, error);
        }
    }

    /// <summary>
    /// Lấy thông tin sử dụng một voucher cụ thể của user đang đăng nhập.
    /// </summary>
    [Authorize]
    [HttpGet("promotion-usages/{promotionId}")]
    [ProducesResponseType(typeof(ApiResult<PromotionUserUsageDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMyPromotionUsage(Guid promotionId)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var result = await _promotionService.GetSpecificPromotionUsagesync(promotionId, userId);
            return Ok(ApiResult<PromotionUserUsageDto>.Success(result, "200", "Lấy thông tin sử dụng voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<PromotionUserUsageDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả voucher đã sử dụng của user đang đăng nhập.
    /// </summary>
    [Authorize]
    [HttpGet("promotion-usages")]
    [ProducesResponseType(typeof(ApiResult<List<PromotionUserUsageDto>>), 200)]
    public async Task<IActionResult> GetMyPromotionUsages()
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var result = await _promotionService.GetPromotionUsageOfUserAsync(userId);
            return Ok(ApiResult<List<PromotionUserUsageDto>>.Success(result, "200", "Lấy danh sách sử dụng voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<PromotionUserUsageDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }


}