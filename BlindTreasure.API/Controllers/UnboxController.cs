using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.DTOs.UnboxLogDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/unboxing")]
[ApiController]
// [Authorize(Roles = "Customer")]
public class UnboxController : ControllerBase
{
    private readonly IUnboxingService _unboxingService;

    public UnboxController(IUnboxingService unboxingService)
    {
        _unboxingService = unboxingService;
    }

    /// <summary>
    ///     Mở hộp Blind Box theo customerBlindBoxId.
    /// </summary>
    [HttpPost("{customerBlindBoxId:guid}")]
    public async Task<IActionResult> UnboxAsync(Guid customerBlindBoxId)
    {
        try
        {
            var result = await _unboxingService.UnboxAsync(customerBlindBoxId);
            return Ok(ApiResult<UnboxResultDto>.Success(result, "200", "Congratulation !"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy danh sách log mở blind box (dành cho Admin/Seller truy xuất đối chiếu)
    /// </summary>
    [HttpGet("unbox-logs")]
    public async Task<IActionResult> GetLogs([FromQuery] PaginationParameter param, [FromQuery] Guid? userId,
        [FromQuery] Guid? productId)
    {
        try
        {
            var result = await _unboxingService.GetLogsAsync(param, userId, productId);

            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }


    /// <summary>
    ///     Lấy danh sách tỷ lệ rơi item đã phê duyệt cho một BlindBox cụ thể.
    /// </summary>
    [HttpGet("probabilities/{blindBoxId:guid}")]
    public async Task<IActionResult> GetApprovedProbabilities(Guid blindBoxId)
    {
        try
        {
            var result = await _unboxingService.GetApprovedProbabilitiesAsync(blindBoxId);
            return Ok(ApiResult<List<ProbabilityConfig>>.Success(result));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}