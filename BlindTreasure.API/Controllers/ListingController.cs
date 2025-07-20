using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/listings")]
[ApiController]
[Authorize]
public class ListingController : ControllerBase
{
    private readonly IListingService _listingService;

    public ListingController(IListingService listingService)
    {
        _listingService = listingService;
    }

    /// <summary>
    ///     Tạo listing (free hoặc trade) cho item trong kho.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateListingRequestDto dto)
    {
        try
        {
            var result = await _listingService.CreateListingAsync(dto);
            return Ok(ApiResult<ListingDto>.Success(result, "200", "Tạo listing thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<ListingDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Báo cáo một listing có dấu hiệu scam.
    /// </summary>
    [HttpPost("{listingId}/report")]
    public async Task<IActionResult> ReportListing(Guid listingId, [FromBody] ReportListingRequest request)
    {
        try
        {
            await _listingService.ReportListingAsync(listingId, request.Reason);
            return Ok(ApiResult<object>.Success(null, "200", "Báo cáo listing thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Lấy danh sách vật phẩm có thể tạo listing.
    /// </summary>
    [HttpGet("available-items")]
    public async Task<IActionResult> GetAvailableItems()
    {
        try
        {
            var result = await _listingService.GetAvailableItemsForListingAsync();
            return Ok(ApiResult<List<InventoryItemDto>>.Success(result, "200", "Lấy danh sách vật phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<InventoryItemDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    // /// <summary>
    // ///     Cronjob: cập nhật các listing quá 30 ngày thành Expired.
    // /// </summary>
    // [HttpPost("cron/expire-old-listings")]
    // [AllowAnonymous]
    // public async Task<IActionResult> ExpireOldListings()
    // {
    //     try
    //     {
    //         var count = await _listingService.ExpireOldListingsAsync();
    //         return Ok(ApiResult<object>.Success(new { count }, "200", "Cập nhật hết hạn listing thành công."));
    //     }
    //     catch (Exception ex)
    //     {
    //         var statusCode = ExceptionUtils.ExtractStatusCode(ex);
    //         var error = ExceptionUtils.CreateErrorResponse<object>(ex);
    //         return StatusCode(statusCode, error);
    //     }
    // }
    /// <summary>
    ///     Tạo Trade Request cho một Listing.
    /// </summary>
    [HttpPost("{listingId}/trade-requests")]
    public async Task<IActionResult> CreateTradeRequest(Guid listingId, [FromBody] CreateTradeRequestDto dto)
    {
        try
        {
            var result = await _listingService.CreateTradeRequestAsync(listingId, dto.OfferedInventoryId);
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
    ///     Phản hồi Trade Request (Accept/Reject).
    /// </summary>
    [HttpPost("trade-requests/{tradeRequestId}/respond")]
    public async Task<IActionResult> RespondTradeRequest(Guid tradeRequestId, [FromQuery] bool isAccepted)
    {
        try
        {
            var result = await _listingService.RespondTradeRequestAsync(tradeRequestId, isAccepted);
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
    ///     Đóng một Listing.
    /// </summary>
    [HttpPost("{listingId}/close")]
    public async Task<IActionResult> CloseListing(Guid listingId)
    {
        try
        {
            var result = await _listingService.CloseListingAsync(listingId);
            return Ok(ApiResult<object>.Success(new { result }, "200", "Đóng listing thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Lấy danh sách Trade Request cho một Listing.
    /// </summary>
    [HttpGet("{listingId}/trade-requests")]
    public async Task<IActionResult> GetTradeRequests(Guid listingId)
    {
        try
        {
            var result = await _listingService.GetTradeRequestsAsync(listingId);
            return Ok(ApiResult<List<TradeRequestDto>>.Success(result, "200", "Lấy danh sách trade requests thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<TradeRequestDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }
}

/// <summary>
/// DTO cho request báo cáo listing.
/// </summary>
public class ReportListingRequest
{
    public string Reason { get; set; }
}