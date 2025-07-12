using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI.ObjectModels.ResponseModels;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("/services/shipment")]
public class ShipmentServiceController : ControllerBase
{
    private readonly IGhtkService _ghtkService;

    public ShipmentServiceController(IGhtkService ghtkService)
    {
        _ghtkService = ghtkService;
    }

    [HttpGet("authenticate")]
    public async Task<IActionResult> Authenticate()
    {
        try
        {
            var res = await _ghtkService.AuthenticateAsync();

            if (!res.Success)
            {
                // Chuyển StatusCode từ string sang int (nếu có)
                if (int.TryParse(res.StatusCode, out var statusCode))
                {
                    var apiResult = new ApiResult<GhtkAuthResponse>
                    {
                        IsSuccess = false,
                        Error = new ErrorContent
                        {
                            Code = res.StatusCode ?? "500",
                            Message = res.Message ?? "GHTK authentication failed."
                        },
                        Value = new ResponseDataContent<GhtkAuthResponse>
                        {
                            Code = res.StatusCode ?? "500",
                            Message = res.Message ?? "GHTK authentication failed.",
                            Data = res
                        }
                    };


                    return StatusCode(statusCode, apiResult);
                }

                // Nếu không parse được, trả về 400 BadRequest
                var failApiResult = ApiResult<GhtkAuthResponse>.Failure(
                    "400",
                    res.Message ?? "GHTK authentication failed."
                );
                return BadRequest(failApiResult);
            }

            //_logger.info("GHTK authentication successful.");
            return Ok(ApiResult<GhtkAuthResponse>.Success(res, "200", "authenticated api ghtk thành công"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<GhtkAuthResponse>.Failure("500", ex.Message));
        }
    }

    [HttpPost("order")]
    public async Task<IActionResult> SubmitOrder([FromBody] GhtkSubmitOrderRequest req)
    {
        var res = await _ghtkService.SubmitOrderAsync(req);
        if (!res.Success)
            return BadRequest(res.Message);
        return Ok(res);
    }


    [HttpGet("track/{trackingOrder}")]
    public async Task<IActionResult> TrackOrder(string trackingOrder)
    {
        var res = await _ghtkService.TrackOrderAsync(trackingOrder);
        if (!res.Success)
            return BadRequest(res.Message);

        return Ok(res);
    }
}