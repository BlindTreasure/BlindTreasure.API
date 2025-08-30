using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.TradeHistoryDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/trading")]
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
    /// Lấy danh sách tất cả trade requests trong hệ thống.
    /// </summary>
    /// <param name="param">
    /// Tham số phân trang, bao gồm: PageIndex, PageSize, Desc (sắp xếp giảm dần).
    /// </param>
    /// <param name="onlyActive">
    /// Nếu = true: chỉ lấy các trade request còn đang đếm ngược (TimeRemaining != 0).  
    /// Nếu = false (mặc định): lấy tất cả trade request.
    /// </param>
    /// <returns>
    /// Trả về danh sách trade requests có phân trang kèm theo thông tin tổng số bản ghi, trang hiện tại, và tổng số trang.
    /// </returns>
    [HttpGet("trade-requests")]
    public async Task<IActionResult> GetAllTradeRequests(
        [FromQuery] PaginationParameter param,
        [FromQuery] bool onlyActive = false) // ✅ thêm query param
    {
        try
        {
            var result = await _tradingService.GetAllTradeRequests(param, onlyActive);

            return Ok(ApiResult<object>.Success(new
                {
                    result,
                    count = result.TotalCount,
                    pageSize = result.PageSize,
                    currentPage = result.CurrentPage,
                    totalPages = result.TotalPages
                }, "200",
                "Lấy danh sách trade requests thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }


    [HttpGet("histories")]
    public async Task<IActionResult> GetAllListings([FromQuery] TradeHistoryQueryParameter param,
        [FromQuery] bool onlyMine)
    {
        try
        {
            var result = await _tradingService.GetTradeHistoriesAsync(param, onlyMine);
            return Ok(ApiResult<object>.Success(new
                {
                    result,
                    count = result.TotalCount,
                    pageSize = result.PageSize,
                    currentPage = result.CurrentPage,
                    totalPages = result.TotalPages
                }, "200",
                "Lấy lịch sử TRADING thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// User B tạo yêu cầu giao dịch với User A cho một Listing.
    /// Nếu Listing miễn phí, User B không cần cung cấp item để trao đổi, nếu không, User B nhập item trong invent
    /// </summary>
    [HttpPost("{listingId}/trade-requests")]
    public async Task<IActionResult> CreateTradeRequest([FromRoute] Guid listingId,
        [FromBody] CreateTradeRequestDto dto)
    {
        // Kiểm tra GUID hợp lệ
        if (listingId == Guid.Empty)
            return BadRequest(ApiResult<TradeRequestDto>.Failure("400", "ID listing không hợp lệ."));

        try
        {
            var result = await _tradingService.CreateTradeRequestAsync(listingId, dto);
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
    /// User A chấp nhận hoặc từ chối yêu cầu giao dịch của User B.
    /// Nếu từ chối, item của User A được cập nhật lại thành Available.
    /// </summary>
    [HttpPost("trade-requests/{tradeRequestId}/respond")]
    public async Task<IActionResult> RespondTradeRequest(Guid tradeRequestId, [FromQuery] bool isAccepted)
    {
        try
        {
            var result = await _tradingService.RespondTradeRequestAsync(tradeRequestId, isAccepted);
            return Ok(ApiResult<TradeRequestDto>.Success(result, "200", "Cập nhật trade request thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// A xem được các requests của 1 listings
    /// </summary>
    [HttpGet("{listingId}/trade-requests")]
    public async Task<IActionResult> GetTradeRequests(Guid listingId)
    {
        try
        {
            var result = await _tradingService.GetTradeRequestsAsync(listingId);
            return Ok(ApiResult<List<TradeRequestDto>>.Success(result, "200",
                "Lấy danh sách trade requests thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<TradeRequestDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Cả A và B đều phải gọi endpoint này để complete giao dịch
    /// </summary>
    [HttpPost("trade-requests/{tradeRequestId}/lock")]
    public async Task<IActionResult> LockDeal(Guid tradeRequestId)
    {
        try
        {
            var result = await _tradingService.LockDealAsync(tradeRequestId);
            return Ok(ApiResult<TradeRequestDto>.Success(result, "200", "Giao dịch đã được khóa thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một trade request theo ID
    /// </summary>
    /// <param name="tradeRequestId">ID của trade request cần lấy thông tin</param>
    /// <returns>Chi tiết của trade request</returns>
    [HttpGet("trade-requests/{tradeRequestId}")]
    public async Task<IActionResult> GetTradeRequestById(Guid tradeRequestId)
    {
        try
        {
            var result = await _tradingService.GetTradeRequestByIdAsync(tradeRequestId);
            return Ok(ApiResult<TradeRequestDto>.Success(result, "200",
                "Lấy thông tin chi tiết trade request thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<TradeRequestDto>(ex);
            return StatusCode(statusCode, error);
        }
    }
}