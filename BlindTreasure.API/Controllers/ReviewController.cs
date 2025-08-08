using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/reviews")]
[ApiController]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Tạo đánh giá mới cho sản phẩm đã mua
    /// </summary>
    /// <param name="createDto">Thông tin đánh giá bao gồm điểm, nội dung và hình ảnh</param>
    /// <returns>Thông tin chi tiết đánh giá đã tạo</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateReview([FromForm] CreateReviewDto createDto)
    {
        try
        {
            var result = await _reviewService.CreateReviewAsync(createDto);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Đánh giá được tạo thành công"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ReviewResponseDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<ReviewResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAll([FromQuery] ReviewQueryParameter param)
    {
        try
        {
            var result = await _reviewService.GetAllReviewsAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Cho phép người bán trả lời đánh giá của khách hàng
    /// </summary>
    /// <param name="reviewId">ID của đánh giá cần phản hồi</param>
    /// <param name="replyDto">Nội dung phản hồi của người bán</param>
    /// <returns>Thông tin đánh giá đã được cập nhật với phản hồi</returns>
    [HttpPost("{reviewId}/reply")]
    [Authorize(Policy = "SellerPolicy")]
    public async Task<IActionResult> ReplyToReview(Guid reviewId, [FromBody] SellerReplyRequestDto replyDto)
    {
        try
        {
            var result = await _reviewService.ReplyToReviewAsync(reviewId, replyDto.Content);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Phản hồi đánh giá thành công"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ReviewResponseDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một đánh giá theo ID
    /// </summary>
    /// <param name="reviewId">ID của đánh giá cần xem</param>
    /// <returns>Thông tin chi tiết của đánh giá</returns>
    [HttpGet("{reviewId}")]
    public async Task<IActionResult> GetReviewDetailsById(Guid reviewId)
    {
        try
        {
            var result = await _reviewService.GetByIdAsync(reviewId);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Xem chi tiết review thành công"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ReviewResponseDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Kiểm tra Customer đã đánh giá chưa
    /// </summary>
    /// <param name="orderDetailId">ID của chi tiết đơn hàng cần kiểm tra</param>
    /// <returns>True nếu có thể đánh giá, False nếu không thể</returns>
    [HttpGet("review-status/{orderDetailId}")]
    public async Task<IActionResult> CanReviewOrderDetail(Guid orderDetailId)
    {
        try
        {
            var hasReviewed = await _reviewService.HasReviewedOrderDetailAsync(orderDetailId);
            return Ok(ApiResult<bool>.Success(hasReviewed, "200",
                hasReviewed ? "Bạn đã đánh giá sản phẩm này rồi." : "Bạn chưa đánh giá sản phẩm này."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<bool>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Cho phép khách hàng xóa đánh giá của bản thân ở một cửa hàng
    /// </summary>
    /// <param name="reviewId">ID của đánh giá cần xóa</param>
    /// <returns>Trả về thông tin của review đó, và True nếu xóa thành công, False với xóa thất bại</returns>
    [HttpDelete("{reviewId}")]
    [Authorize(Policy = "CustomerPolicy")]
    public async Task<IActionResult> DeleteReview(Guid reviewId)
    {
        try
        {
            var result = await _reviewService.DeleteReviewAsync(reviewId);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200",
                "Bạn đã xóa bài đánh giá cho đơn hàng này thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<bool>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}