using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/blindy")]
public class BlindyController : ControllerBase
{
    private readonly IBlindyService _blindyService;

    public BlindyController(IBlindyService blindyService)
    {
        _blindyService = blindyService;
    }

    [HttpPost("ask-gemini")]
    public async Task<IActionResult> AskGemini([FromBody] string prompt)
    {
        try
        {
            var response = await _blindyService.AskGeminiAsync(prompt);
            return Ok(ApiResult<string>.Success(response, "200", "Phản hồi từ Gemini đã được tải thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<string>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Staff gọi endpoint này để phân tích AI về thông tin người dùng gần nhất trong hệ thống.
    /// </summary>
    [HttpGet("analyze-users")]
    public async Task<IActionResult> AnalyzeUsers()
    {
        try
        {
            var result = await _blindyService.AnalyzeUsersWithAi();
            return Ok(ApiResult<string>.Success(result, "200", "Phân tích người dùng bằng AI đã hoàn tất."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<string>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    ///     Staff gọi endpoint này để phân tích AI danh sách sản phẩm.
    /// </summary>
    [HttpGet("analyze-products")]
    public async Task<IActionResult> AnalyzeProducts()
    {
        try
        {
            var result = await _blindyService.GetProductsForAiAnalysisAsync();
            return Ok(ApiResult<string>.Success(result, "200", "Phân tích sản phẩm bằng AI đã hoàn tất."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<string>.Failure("500", ex.Message));
        }
    }
}