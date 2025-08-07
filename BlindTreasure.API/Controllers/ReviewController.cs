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
    public async Task<IActionResult> CreateReview([FromForm] CreateReviewDto createDto)
    {
        try
        {
            var result = await _reviewService.CreateReviewAsync(createDto);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Tạo đánh giá thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ReviewResponseDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
   
}