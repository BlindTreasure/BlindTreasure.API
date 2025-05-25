using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;
using BlindTreasure.Application.Utils;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/blindy")]
public class BlindyController : ControllerBase
{
    private readonly IGptService _gptService;

    public BlindyController(IGptService gptService)
    {
        _gptService = gptService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] string prompt)
    {
        try
        {
            var response = await _gptService.GenerateResponseAsync(prompt);
            return Ok(ApiResult<string>.Success(response));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<string>.Failure("500", ex.Message));
        }
    }
}