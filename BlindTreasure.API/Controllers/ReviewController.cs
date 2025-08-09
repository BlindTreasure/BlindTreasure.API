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
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IReviewService reviewService, ILogger<ReviewController> logger)
    {
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Tạo đánh giá mới cho sản phẩm đã mua
    /// </summary>
    /// <param name="createDto">Thông tin đánh giá bao gồm điểm, nội dung và hình ảnh</param>
    /// <returns>Thông tin chi tiết đánh giá đã tạo</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Authorize(Policy = "CustomerPolicy")] // THÊM AUTHORIZATION
    public async Task<IActionResult> CreateReview([FromForm] CreateReviewDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating review for OrderDetail: {OrderDetailId}", createDto?.OrderDetailId);

            // VALIDATE INPUT TRƯỚC KHI GỌI SERVICE
            if (createDto == null)
            {
                _logger.LogWarning("CreateReviewDto is null");
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "Dữ liệu đánh giá không hợp lệ"));
            }

            if (createDto.OrderDetailId == Guid.Empty)
            {
                _logger.LogWarning("Empty OrderDetailId in CreateReview");
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "ID chi tiết đơn hàng không hợp lệ"));
            }

            if (createDto.Rating < 1 || createDto.Rating > 5)
            {
                _logger.LogWarning("Invalid rating: {Rating}", createDto.Rating);
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "Điểm đánh giá phải từ 1 đến 5"));
            }

            if (string.IsNullOrWhiteSpace(createDto.Comment))
            {
                _logger.LogWarning("Empty comment in CreateReview");
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "Nội dung đánh giá không được để trống"));
            }

            var result = await _reviewService.CreateReviewAsync(createDto);
            
            _logger.LogInformation("Successfully created review with ID: {ReviewId}", result?.Id);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Đánh giá được tạo thành công"));
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "ArgumentNullException in CreateReview");
            return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "Dữ liệu đầu vào không hợp lệ"));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access in CreateReview");
            return Unauthorized(ApiResult<ReviewResponseDto>.Failure("401", "Bạn cần đăng nhập để thực hiện hành động này"));
        }
        catch (ApplicationException ex) when (ex.Data.Contains("StatusCode"))
        {
            var statusCode = (int)ex.Data["StatusCode"]!;
            _logger.LogWarning(ex, "Application exception with status {StatusCode} in CreateReview", statusCode);
            
            var errorResponse = ApiResult<ReviewResponseDto>.Failure(statusCode.ToString(), ex.Message);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in CreateReview for OrderDetail: {OrderDetailId}", createDto?.OrderDetailId);
            
            var errorResponse = ApiResult<ReviewResponseDto>.Failure("500", "Đã xảy ra lỗi hệ thống khi tạo đánh giá");
            return StatusCode(500, errorResponse);
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<ReviewResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAll([FromQuery] ReviewQueryParameter param)
    {
        try
        {
            _logger.LogInformation("Getting reviews with parameters: ProductId={ProductId}, BlindBoxId={BlindBoxId}", 
                param?.ProductId, param?.BlindBoxId);

            // VALIDATE PARAM
            if (param == null)
            {
                param = new ReviewQueryParameter(); // Sử dụng default values
            }

            // Validate pagination parameters
            if (param.PageIndex < 1)
            {
                param.PageIndex = 1;
            }

            if (param.PageSize < 1 || param.PageSize > 100)
            {
                param.PageSize = 10; // Default page size
            }

            var result = await _reviewService.GetAllReviewsAsync(param);
            
            _logger.LogInformation("Successfully retrieved {Count} reviews", result?.TotalCount ?? 0);
            
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result?.TotalCount ?? 0,
                pageSize = result?.PageSize ?? param.PageSize,
                currentPage = result?.CurrentPage ?? param.PageIndex,
                totalPages = result?.TotalPages ?? 0
            }, "200", "Lấy danh sách thành công."));
        }
        catch (ApplicationException ex) when (ex.Data.Contains("StatusCode"))
        {
            var statusCode = (int)ex.Data["StatusCode"]!;
            _logger.LogWarning(ex, "Application exception with status {StatusCode} in GetAll", statusCode);
            
            var errorResponse = ApiResult<object>.Failure(statusCode.ToString(), ex.Message);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetAll reviews");
            
            var errorResponse = ApiResult<object>.Failure("500", "Đã xảy ra lỗi hệ thống khi lấy danh sách đánh giá");
            return StatusCode(500, errorResponse);
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
            _logger.LogInformation("Seller replying to review: {ReviewId}", reviewId);

            // VALIDATE INPUT
            if (reviewId == Guid.Empty)
            {
                _logger.LogWarning("Empty reviewId in ReplyToReview");
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "ID đánh giá không hợp lệ"));
            }

            if (replyDto == null || string.IsNullOrWhiteSpace(replyDto.Content))
            {
                _logger.LogWarning("Empty reply content in ReplyToReview");
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "Nội dung phản hồi không được để trống"));
            }

            if (replyDto.Content.Length > 1000)
            {
                _logger.LogWarning("Reply content too long: {Length} characters", replyDto.Content.Length);
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "Nội dung phản hồi không được vượt quá 1000 ký tự"));
            }

            var result = await _reviewService.ReplyToReviewAsync(reviewId, replyDto.Content);
            
            _logger.LogInformation("Successfully replied to review: {ReviewId}", reviewId);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Phản hồi đánh giá thành công"));
        }
        catch (ApplicationException ex) when (ex.Data.Contains("StatusCode"))
        {
            var statusCode = (int)ex.Data["StatusCode"]!;
            _logger.LogWarning(ex, "Application exception with status {StatusCode} in ReplyToReview", statusCode);
            
            var errorResponse = ApiResult<ReviewResponseDto>.Failure(statusCode.ToString(), ex.Message);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ReplyToReview for ReviewId: {ReviewId}", reviewId);
            var statusCode = (int)ex.Data["StatusCode"]!;
            var errorResponse = ApiResult<ReviewResponseDto>.Failure("500", "Đã xảy ra lỗi hệ thống khi phản hồi đánh giá");
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
            _logger.LogInformation("Getting review details for ID: {ReviewId}", reviewId);

            // VALIDATE INPUT
            if (reviewId == Guid.Empty)
            {
                _logger.LogWarning("Empty reviewId in GetReviewDetailsById");
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "ID đánh giá không hợp lệ"));
            }

            var result = await _reviewService.GetByIdAsync(reviewId);
            
            _logger.LogInformation("Successfully retrieved review details: {ReviewId}", reviewId);
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200", "Xem chi tiết review thành công"));
        }
        catch (ApplicationException ex) when (ex.Data.Contains("StatusCode"))
        {
            var statusCode = (int)ex.Data["StatusCode"]!;
            _logger.LogWarning(ex, "Application exception with status {StatusCode} in GetReviewDetailsById", statusCode);
            
            var errorResponse = ApiResult<ReviewResponseDto>.Failure(statusCode.ToString(), ex.Message);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetReviewDetailsById for ReviewId: {ReviewId}", reviewId);
            
            var errorResponse = ApiResult<ReviewResponseDto>.Failure("500", "Đã xảy ra lỗi hệ thống khi lấy chi tiết đánh giá");
            return StatusCode(500, errorResponse);
        }
    }

    /// <summary>
    /// Kiểm tra Customer đã đánh giá chưa
    /// </summary>
    /// <param name="orderDetailId">ID của chi tiết đơn hàng cần kiểm tra</param>
    /// <returns>True nếu đã đánh giá, False nếu chưa đánh giá</returns>
    [HttpGet("review-status/{orderDetailId}")]
    [Authorize(Policy = "CustomerPolicy")] // THÊM AUTHORIZATION
    public async Task<IActionResult> CheckReviewStatus(Guid orderDetailId)
    {
        try
        {
            _logger.LogInformation("Checking review status for OrderDetail: {OrderDetailId}", orderDetailId);

            // VALIDATE INPUT
            if (orderDetailId == Guid.Empty)
            {
                _logger.LogWarning("Empty orderDetailId in CheckReviewStatus");
                return BadRequest(ApiResult<bool>.Failure("400", "ID chi tiết đơn hàng không hợp lệ"));
            }

            var hasReviewed = await _reviewService.HasReviewedOrderDetailAsync(orderDetailId);
            
            _logger.LogInformation("Review status for OrderDetail {OrderDetailId}: {HasReviewed}", orderDetailId, hasReviewed);
            
            return Ok(ApiResult<bool>.Success(hasReviewed, "200",
                hasReviewed ? "Bạn đã đánh giá sản phẩm này rồi." : "Bạn chưa đánh giá sản phẩm này."));
        }
        catch (ApplicationException ex) when (ex.Data.Contains("StatusCode"))
        {
            var statusCode = (int)ex.Data["StatusCode"]!;
            _logger.LogWarning(ex, "Application exception with status {StatusCode} in CheckReviewStatus", statusCode);
            
            var errorResponse = ApiResult<bool>.Failure(statusCode.ToString(), ex.Message);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in CheckReviewStatus for OrderDetail: {OrderDetailId}", orderDetailId);
            
            var errorResponse = ApiResult<bool>.Failure("500", "Đã xảy ra lỗi hệ thống khi kiểm tra trạng thái đánh giá");
            return StatusCode(500, errorResponse);
        }
    }

    /// <summary>
    /// Cho phép khách hàng xóa đánh giá của bản thân
    /// </summary>
    /// <param name="reviewId">ID của đánh giá cần xóa</param>
    /// <returns>Trả về thông tin của review đó và trạng thái xóa</returns>
    [HttpDelete("{reviewId}")]
    [Authorize(Policy = "CustomerPolicy")]
    public async Task<IActionResult> DeleteReview(Guid reviewId)
    {
        try
        {
            _logger.LogInformation("Deleting review: {ReviewId}", reviewId);

            // VALIDATE INPUT
            if (reviewId == Guid.Empty)
            {
                _logger.LogWarning("Empty reviewId in DeleteReview");
                return BadRequest(ApiResult<ReviewResponseDto>.Failure("400", "ID đánh giá không hợp lệ"));
            }

            var result = await _reviewService.DeleteReviewAsync(reviewId);
            
            _logger.LogInformation("Successfully deleted review: {ReviewId}", reviewId);
            
            return Ok(ApiResult<ReviewResponseDto>.Success(result, "200",
                "Bạn đã xóa bài đánh giá cho đơn hàng này thành công."));
        }
        catch (ApplicationException ex) when (ex.Data.Contains("StatusCode"))
        {
            var statusCode = (int)ex.Data["StatusCode"]!;
            _logger.LogWarning(ex, "Application exception with status {StatusCode} in DeleteReview", statusCode);
            
            var errorResponse = ApiResult<ReviewResponseDto>.Failure(statusCode.ToString(), ex.Message);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in DeleteReview for ReviewId: {ReviewId}", reviewId);
            
            // SỬA LỖI: return type phải match với method signature
            var errorResponse = ApiResult<ReviewResponseDto>.Failure("500", "Đã xảy ra lỗi hệ thống khi xóa đánh giá");
            return StatusCode(500, errorResponse);
        }
    }
}