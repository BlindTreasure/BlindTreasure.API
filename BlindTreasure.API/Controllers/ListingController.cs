using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;
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
    /// Tạo listing bán lại cho item đã mở trong kho.
    /// Yêu cầu item thuộc user hiện tại, lấy từ blind box, chưa có listing đang hoạt động.
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
    /// Lấy danh sách vật phẩm trong kho mà user có thể tạo listing (đã mở từ blind box, chưa có listing hoạt động).
    /// </summary>
    [HttpGet("available-items")]
    public async Task<IActionResult> GetAvailableItems()
    {
        try
        {
            var result = await _listingService.GetAvailableItemsForListingAsync();
            return Ok(ApiResult<List<InventoryItemDto>>.Success(result, "200", "Lấy danh sách vật phẩm có thể bán lại thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<InventoryItemDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Tính toán giá gợi ý trên thị trường cho một sản phẩm dựa trên các listing đang hoạt động.
    /// </summary>
    [HttpGet("suggested-price/{productId}")]
    public async Task<IActionResult> GetSuggestedPrice(Guid productId)
    {
        try
        {
            var result = await _listingService.GetSuggestedPriceAsync(productId);
            return Ok(ApiResult<decimal>.Success(result, "200", "Lấy giá gợi ý thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<decimal>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Lấy lịch sử biến động giá theo thời gian của một sản phẩm từ Redis cache.
    /// Dữ liệu phục vụ cho hiển thị biểu đồ giá động.
    /// </summary>
    [HttpGet("price-history/{productId}")]
    public async Task<IActionResult> GetPriceHistory(Guid productId)
    {
        try
        {
            var result = await _listingService.GetPriceHistoryAsync(productId);
            return Ok(ApiResult<List<PricePointDto>>.Success(result, "200", "Lấy lịch sử biến động giá thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<List<PricePointDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Cronjob: cập nhật các listing đã quá 30 ngày kể từ ListedAt thành trạng thái Expired.
    /// </summary>
    [HttpPost("cron/expire-old-listings")]
    [AllowAnonymous] // nếu chạy bằng cronjob, có thể bỏ nếu bảo vệ bằng Auth nội bộ
    public async Task<IActionResult> ExpireOldListings()
    {
        try
        {
            var count = await _listingService.ExpireOldListingsAsync();
            return Ok(ApiResult<object>.Success(new { count }, "200", "Cập nhật hết hạn listing thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }
}
