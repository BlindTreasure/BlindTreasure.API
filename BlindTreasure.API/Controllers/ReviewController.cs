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

    [HttpGet("can-review/{orderDetailId}")]
    public async Task<IActionResult> CanReviewOrderDetail(Guid orderDetailId)
    {
        try
        {
            var canReview = await _reviewService.CanReviewOrderDetailAsync(orderDetailId);
            return Ok(ApiResult<bool>.Success(canReview, "200", "Ok bạn có thể để lại đánh giá cho đơn hàng này"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<bool>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}