using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.Entities;
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