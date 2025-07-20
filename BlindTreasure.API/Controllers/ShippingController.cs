using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlindTreasure.API.Controllers;

/// <summary>
///     API tích hợp GHN Shipping cho BlindTreasure.
///     Cung cấp các endpoint lấy địa chỉ, dịch vụ, tính phí, preview và tạo đơn hàng GHN.
/// </summary>
[ApiController]
[Route("api/shipping")]
public class ShippingController : ControllerBase
{
    private readonly IGhnShippingService _shippingService;
    private readonly ILoggerService _logger;

    public ShippingController(IGhnShippingService shippingService, ILoggerService logger)
    {
        _shippingService = shippingService;
        _logger = logger;
    }

    /// <summary>
    ///     Lấy danh sách tỉnh/thành phố từ GHN.
    /// </summary>
    [HttpGet("provinces")]
    [ProducesResponseType(typeof(ApiResult<List<ProvinceDto>>), 200)]
    public async Task<IActionResult> GetProvinces()
    {
        try
        {
            var result = await _shippingService.GetProvincesAsync();
            if (result == null)
                return StatusCode(500, ApiResult<List<ProvinceDto>>.Failure("500", "GHN error or no data."));
            return Ok(ApiResult<List<ProvinceDto>>.Success(result, "200", "Lấy danh sách tỉnh/thành thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[ShippingController][GetProvinces] {ex.Message}");
            return StatusCode(500, ApiResult<List<ProvinceDto>>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Lấy danh sách quận/huyện theo tỉnh/thành phố từ GHN.
    /// </summary>
    [HttpGet("districts")]
    [ProducesResponseType(typeof(ApiResult<List<DistrictDto>>), 200)]
    public async Task<IActionResult> GetDistricts([FromQuery] int provinceId)
    {
        try
        {
            var result = await _shippingService.GetDistrictsAsync(provinceId);
            if (result == null)
                return StatusCode(500, ApiResult<List<DistrictDto>>.Failure("500", "GHN error or no data."));
            return Ok(ApiResult<List<DistrictDto>>.Success(result, "200", "Lấy danh sách quận/huyện thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[ShippingController][GetDistricts] {ex.Message}");
            return StatusCode(500, ApiResult<List<DistrictDto>>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Lấy danh sách phường/xã theo quận/huyện từ GHN.
    /// </summary>
    [HttpGet("wards")]
    [ProducesResponseType(typeof(ApiResult<List<WardDto>>), 200)]
    public async Task<IActionResult> GetWards([FromQuery] int districtId)
    {
        try
        {
            var result = await _shippingService.GetWardsAsync(districtId);
            if (result == null)
                return StatusCode(500, ApiResult<List<WardDto>>.Failure("500", "GHN error or no data."));
            return Ok(ApiResult<List<WardDto>>.Success(result, "200", "Lấy danh sách phường/xã thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[ShippingController][GetWards] {ex.Message}");
            return StatusCode(500, ApiResult<List<WardDto>>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Lấy danh sách dịch vụ vận chuyển GHN giữa 2 quận/huyện.
    /// </summary>
    [HttpGet("available-services")]
    [ProducesResponseType(typeof(ApiResult<List<ServiceDTO>>), 200)]
    public async Task<IActionResult> GetAvailableServices([FromQuery] int fromDistrict, [FromQuery] int toDistrict)
    {
        try
        {
            var result = await _shippingService.GetAvailableServicesAsync(fromDistrict, toDistrict);
            if (result == null)
                return StatusCode(500, ApiResult<List<ServiceDTO>>.Failure("500", "GHN error or no data."));
            return Ok(ApiResult<List<ServiceDTO>>.Success(result, "200", "Lấy dịch vụ vận chuyển thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[ShippingController][GetAvailableServices] {ex.Message}");
            return StatusCode(500, ApiResult<List<ServiceDTO>>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Tính phí vận chuyển GHN.
    /// </summary>
    [HttpPost("calculate-fee")]
    [ProducesResponseType(typeof(ApiResult<CalculateShippingFeeResponse>), 200)]
    public async Task<IActionResult> CalculateFee([FromBody] CalculateShippingFeeRequest request)
    {
        try
        {
            var result = await _shippingService.CalculateFeeAsync(request);
            if (result == null)
                return StatusCode(500, ApiResult<CalculateShippingFeeResponse>.Failure("500", "GHN error or no data."));
            return Ok(ApiResult<CalculateShippingFeeResponse>.Success(result, "200",
                "Tính phí vận chuyển thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[ShippingController][CalculateFee] {ex.Message}");
            return StatusCode(500, ApiResult<CalculateShippingFeeResponse>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Xem trước thông tin đơn hàng GHN (preview).
    /// </summary>
    [HttpPost("preview-order")]
    [ProducesResponseType(typeof(ApiResult<GhnPreviewResponse>), 200)]
    public async Task<IActionResult> PreviewOrder([FromBody] GhnOrderRequest req)
    {
        try
        {
            var result = await _shippingService.PreviewOrderAsync(req);
            if (result == null)
                return StatusCode(500, ApiResult<GhnPreviewResponse>.Failure("500", "GHN error or no data."));
            return Ok(ApiResult<GhnPreviewResponse>.Success(result, "200", "Preview đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[ShippingController][PreviewOrder] {ex.Message}");
            return StatusCode(500, ApiResult<GhnPreviewResponse>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Tạo đơn hàng chính thức trên GHN.
    /// </summary>
    [HttpPost("create-order")]
    [ProducesResponseType(typeof(ApiResult<GhnCreateResponse>), 200)]
    public async Task<IActionResult> CreateOrder([FromBody] GhnOrderRequest req)
    {
        try
        {
            var result = await _shippingService.CreateOrderAsync(req);
            if (result == null)
                return StatusCode(500, ApiResult<GhnCreateResponse>.Failure("500", "GHN error or no data."));
            return Ok(ApiResult<GhnCreateResponse>.Success(result, "200", "Tạo đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[ShippingController][CreateOrder] {ex.Message}");
            return StatusCode(500, ApiResult<GhnCreateResponse>.Failure("500", ex.Message));
        }
    }
}