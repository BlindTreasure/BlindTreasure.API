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
            return Ok(ApiResult<string>.Success(response));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<string>.Failure("500", ex.Message));
        }
    }
}