using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/trades")]
[ApiController]
[Authorize]
public class TradingController : ControllerBase
{
    private readonly ITradingService _tradingService;

    public TradingController(ITradingService tradingService)
    {
        _tradingService = tradingService;
    }

    /// <summary>
    /// Tạo Trade Request cho một Listing.
    /// </summary>
    [HttpPost("{listingId}/trade-requests")]
    public async Task<IActionResult> CreateTradeRequest(Guid listingId, [FromBody] CreateTradeRequestDto dto)
    {
        try
        {
            var result = await _tradingService.CreateTradeRequestAsync(listingId, dto.OfferedInventoryId);
            return Ok(ApiResult<TradeRequestDto>.Success(result, "200", "Tạo trade request thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<TradeRequestDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Phản hồi Trade Request (Accept/Reject).
    /// </summary>
    [HttpPost("trade-requests/{tradeRequestId}/respond")]
    public async Task<IActionResult> RespondTradeRequest(Guid tradeRequestId, [FromQuery] bool isAccepted)
    {
        try
        {
            var result = await _tradingService.RespondTradeRequestAsync(tradeRequestId, isAccepted);
            return Ok(ApiResult<object>.Success(new { result }, "200", "Cập nhật trade request thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Lấy danh sách Trade Request cho một Listing.
    /// </summary>
    [HttpGet("{listingId}/trade-requests")]
    public async Task<IActionResult> GetTradeRequests(Guid listingId)
    {
        try
        {
            var result = await _tradingService.GetTradeRequestsAsync(listingId);
            return Ok(ApiResult<List<TradeRequestDto>>.Success(result, "200", "Lấy danh sách trade requests thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<TradeRequestDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Bắt đầu giao dịch (lock deal).
    /// </summary>
    [HttpPost("trade-requests/{tradeRequestId}/lock")]
    public async Task<IActionResult> LockDeal(Guid tradeRequestId)
    {
        try
        {
            var result = await _tradingService.LockDealAsync(tradeRequestId);
            return Ok(ApiResult<object>.Success(new { result }, "200", "Giao dịch đã được khóa thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Xác nhận giao dịch.
    /// </summary>
    [HttpPost("trade-requests/{tradeRequestId}/confirm")]
    public async Task<IActionResult> ConfirmDeal(Guid tradeRequestId)
    {
        try
        {
            var result = await _tradingService.ConfirmDealAsync(tradeRequestId);
            return Ok(ApiResult<object>.Success(new { result }, "200", "Giao dịch đã được xác nhận thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Hủy giao dịch hoặc hết hạn.
    /// </summary>
    [HttpPost("trade-requests/{tradeRequestId}/expire")]
    public async Task<IActionResult> ExpireDeal(Guid tradeRequestId)
    {
        try
        {
            var result = await _tradingService.ExpireDealAsync(tradeRequestId);
            return Ok(ApiResult<object>.Success(new { result }, "200", "Giao dịch đã hết hạn."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }
}

