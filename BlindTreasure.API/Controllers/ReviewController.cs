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
    ///     Tạo đánh giá mới
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ReviewResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ReviewResponseDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<ReviewResponseDto>), 404)]
    [ProducesResponseType(typeof(ApiResult<ReviewResponseDto>), 409)]
    public async Task<IActionResult> CreateReview([FromForm] CreateReviewDto createDto)
    {
        try
        {
            // Get userId from claims
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

            var result = await _reviewService.CreateReviewAsync(userId, createDto);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Tạo đánh giá thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ReviewResponseDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy danh sách đánh giá (có phân trang)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<ReviewResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllReviews([FromQuery] ReviewQueryParameter param)
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
            }, "200", "Lấy danh sách đánh giá thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy chi tiết đánh giá theo ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult<ReviewResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ReviewResponseDto>), 404)]
    public async Task<IActionResult> GetReviewById(Guid id)
    {
        try
        {
            var result = await _reviewService.GetReviewByIdAsync(id);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Lấy thông tin đánh giá thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ReviewResponseDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}