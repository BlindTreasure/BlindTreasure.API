using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Infrastructure.Commons;
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

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAllListings([FromQuery] ListingQueryParameter param)
    {
        try
        {
            var result = await _listingService.GetAllListingsAsync(param);
            return Ok(ApiResult<Pagination<ListingDetailDto>>.Success(result, "200",
                "Lấy danh sách listing thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<Pagination<ListingDetailDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    [HttpGet("{listingId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetListingDetails(Guid listingId)
    {
        try
        {
            var result = await _listingService.GetByIdAsync(listingId);
            return Ok(ApiResult<object>.Success(result, "200",
                "Lấy chi tiết listing thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
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
            return Ok(ApiResult<object>.Success(result, "200", "Tạo listing thành công."));
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
}

/// <summary>
/// DTO cho request báo cáo listing.
/// </summary>
public class ReportListingRequest
{
    public string Reason { get; set; }
}