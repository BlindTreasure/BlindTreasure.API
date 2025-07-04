using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PromotionDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/promotions")]
[ApiController]
public class PromotionController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    /// <summary>
    ///     TẠO VOUCHER: SELLER THÌ PENDING; STAFF THÌ APPROVED
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Seller,Staff")]
    public async Task<IActionResult> CreatePromotion([FromForm] CreatePromotionDto dto)
    {
        try
        {
            var result = await _promotionService.CreatePromotionAsync(dto);
            return Ok(ApiResult<PromotionDto>.Success(result, "200", "Gửi yêu cầu tạo voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<PromotionDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Lấy danh sách voucher
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<PromotionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPromotions([FromQuery] PromotionQueryParameter param)
    {
        try
        {
            var result = await _promotionService.GetPromotionsAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<PromotionDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Lấy details voucher
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPromotionById(Guid id)
    {
        try
        {
            var result = await _promotionService.GetPromotionByIdAsync(id);
            return Ok(ApiResult<object>.Success(result, "200", "Lấy voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<PromotionDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     STAFF duyệt hoặc từ chối voucher đang chờ xử lý.
    /// </summary>
    [Authorize(Roles = "Staff")]
    [HttpPost("review")]
    [ProducesResponseType(typeof(ApiResult<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewPromotion([FromBody] ReviewPromotionDto dto)
    {
        try
        {
            var result = await _promotionService.ReviewPromotionAsync(dto);
            return Ok(ApiResult<PromotionDto>.Success(result, "200", "Xử lý xét duyệt voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<PromotionDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// STAFF cập nhật thông tin voucher
    /// </summary>
    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResult<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePromotion(Guid id, [FromForm] CreatePromotionDto dto)
    {
        try
        {
            var result = await _promotionService.UpdatePromotionAsync(id, dto);
            return Ok(ApiResult<PromotionDto>.Success(result, "200", "Cập nhật voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<PromotionDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// STAFF xoá mềm voucher
    /// </summary>
    [Authorize(Roles = "Staff,Admin")]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePromotion(Guid id)
    {
        try
        {
            var success = await _promotionService.DeletePromotionAsync(id);
            return Ok(ApiResult<object>.Success(success, "200", "Xoá voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }
}