using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PromotionDTOs;
using BlindTreasure.Infrastructure.Commons;
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
    /// TẠO VOUCHER: SELLER THÌ PENDING; STAFF THÌ APPROVED
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status400BadRequest)]
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
    /// Lấy danh sách voucher của Seller hiện tại (có filter theo Status, phân trang).
    /// </summary>
    [HttpGet()]
    [ProducesResponseType(typeof(ApiResult<Pagination<PromotionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPromotions([FromQuery] PromotionQueryParameter param)
    {
        try
        {
            var result = await _promotionService.GetPromotionsAsync(param);
            return Ok(ApiResult<Pagination<PromotionDto>>.Success(result, "200", "Lấy danh sách voucher thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<PromotionDto>(ex);
            return StatusCode(statusCode, error);
        }
    }
}