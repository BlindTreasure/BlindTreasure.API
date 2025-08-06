using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IBlindyService _blindyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoggerService _loggerService;

    public ReviewService(IBlindyService blindyService, IUnitOfWork unitOfWork, ILoggerService loggerService)
    {
        _blindyService = blindyService;
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
    }

    public async Task<ReviewResponseDto> CreateReviewAsync(Guid userId, CreateReviewDto createDto)
    {
        // 1. Validate input and permissions
        var orderDetail = await ValidateReviewCreationAsync(userId, createDto);

        // 2. AI validate nội dung
        var validation = await ValidateReviewContentWithAiAsync(createDto, orderDetail);

        // 3. Create and save review
        var review = await CreateAndSaveReviewAsync(userId, createDto, orderDetail, validation);

        // 4. Load and return response
        return await LoadReviewForResponseAsync(review.Id);
    }

    public async Task<Pagination<ReviewResponseDto>> GetAllReviewsAsync(ReviewQueryParameter param)
    {
        var baseQuery = _unitOfWork.Reviews.GetQueryable()
            .Include(r => r.User)
            .Include(r => r.Product)
            .Include(r => r.Product!.Category) // Thêm này
            .Include(r => r.BlindBox)
            .Include(r => r.BlindBox!.Category) // Thêm này nếu BlindBox có Category
            .Include(r => r.Seller)
            .Where(r =>
                (param.IsPublished == null || r.IsPublished == param.IsPublished) &&
                (param.ProductId == null || r.ProductId == param.ProductId) &&
                (param.BlindBoxId == null || r.BlindBoxId == param.BlindBoxId) &&
                (param.SellerId == null || r.SellerId == param.SellerId) &&
                (param.UserId == null || r.UserId == param.UserId) &&
                (param.Status == null || r.Status == param.Status) &&
                (param.MinRating == null || r.OverallRating >= param.MinRating) &&
                (param.IsVerifiedPurchase == null || r.IsVerifiedPurchase == param.IsVerifiedPurchase)
            )
            .AsNoTracking();

        var query = baseQuery.OrderByDescending(r => r.CreatedAt);
        var count = await query.CountAsync();

        List<Review> items;
        if (param.PageIndex == 0)
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(MapReviewToDto).ToList();
        var result = new Pagination<ReviewResponseDto>(dtos, count, param.PageIndex, param.PageSize);

        return result;
    }

    public async Task<ReviewResponseDto> GetReviewByIdAsync(Guid reviewId)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId,
            r => r.User,
            r => r.Product,
            r => r.Product.Category, // Thêm này
            r => r.BlindBox,
            r => r.BlindBox.Category, // Thêm này nếu BlindBox có Category
            r => r.Seller
        );

        if (review == null)
            throw ErrorHelper.NotFound("Không tìm thấy đánh giá.");

        if (!review.IsPublished)
            throw ErrorHelper.NotFound("Đánh giá không khả dụng.");

        return MapReviewToDto(review);
    }


    #region private methods

    private async Task<dynamic> ValidateReviewContentWithAiAsync(CreateReviewDto createDto, OrderDetail orderDetail)
    {
        try
        {
            var validation = await _blindyService.ValidateReviewAsync(
                createDto.Comment,
                createDto.OverallRating,
                orderDetail.Seller?.CompanyName,
                orderDetail.Product?.Name ?? orderDetail.BlindBox?.Name
            );

            _loggerService.Info(
                $"AI validation completed for review. Valid: {validation.IsValid}, Action: {validation.SuggestedAction}");

            return validation;
        }
        catch (Exception ex)
        {
            _loggerService.Error(
                $"AI validation failed for review: {ex.Message}");

            // Fallback: cho phép review nhưng cần moderation
            return new
            {
                IsValid = true,
                SuggestedAction = "moderate",
                CleanedComment = createDto.Comment,
                Reason = "AI validation unavailable"
            };
        }
    }

    private async Task<Review> CreateAndSaveReviewAsync(
        Guid userId,
        CreateReviewDto createDto,
        OrderDetail orderDetail,
        dynamic validation)
    {
        var review = BuildReviewFromValidation(userId, createDto, orderDetail, validation);

        await _unitOfWork.Reviews.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Info(
            $"Review created successfully. ID: {review.Id}, Status: {review.Status}");

        return review;
    }

    private Review BuildReviewFromValidation(
        Guid userId,
        CreateReviewDto createDto,
        OrderDetail orderDetail,
        dynamic validation)
    {
        var reviewStatus = DetermineReviewStatus(validation.SuggestedAction);
        var isPublished = validation.SuggestedAction == "approve";

        return new Review
        {
            UserId = userId,
            OrderDetailId = createDto.OrderDetailId,
            ProductId = orderDetail.ProductId,
            BlindBoxId = orderDetail.BlindBoxId,
            SellerId = orderDetail.SellerId,
            OverallRating = createDto.OverallRating,
            OriginalComment = createDto.Comment,
            ProcessedComment = validation.CleanedComment ?? createDto.Comment,
            IsCommentValid = validation.IsValid,
            ValidationReason = validation.Reason,
            ImageUrls = createDto.ImageUrls ?? new List<string>(),
            AiValidatedAt = DateTime.UtcNow,
            AiValidationDetails = JsonSerializer.Serialize(validation),
            Status = reviewStatus,
            IsPublished = isPublished,
            IsVerifiedPurchase = true
        };
    }

    private ReviewStatus DetermineReviewStatus(string suggestedAction)
    {
        return suggestedAction switch
        {
            "approve" => ReviewStatus.Approved,
            "moderate" => ReviewStatus.RequiresModeration,
            "reject" => ReviewStatus.Rejected,
            _ => ReviewStatus.RequiresModeration
        };
    }

    private async Task<ReviewResponseDto> LoadReviewForResponseAsync(Guid reviewId)
    {
        var createdReview = await _unitOfWork.Reviews.GetByIdAsync(reviewId,
            r => r.User,
            r => r.Product,
            r => r.Product.Category,
            r => r.BlindBox,
            r => r.BlindBox.Category,
            r => r.Seller
        );

        if (createdReview == null)
            throw ErrorHelper.Internal("Không thể tải thông tin đánh giá vừa tạo.");

        return MapReviewToDto(createdReview);
    }

    private ReviewResponseDto MapReviewToDto(Review review)
    {
        return new ReviewResponseDto
        {
            Id = review.Id,
            UserId = review.UserId, // Thêm UserId
            UserName = review.User?.FullName ?? "Ẩn danh",
            UserAvatarUrl = review.User?.AvatarUrl,
            ProductName = review.Product?.Name,
            BlindBoxName = review.BlindBox?.Name,
            SellerName = review.Seller?.CompanyName ?? "Không rõ",
            OverallRating = review.OverallRating, // Thêm rating
            Comment = review.ProcessedComment ?? review.OriginalComment,
            Category = review.Product?.Category?.Name ?? review.BlindBox?.Category?.Name, // Thêm category
            ImageUrls = review.ImageUrls ?? new List<string>(),
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            SellerReply = !string.IsNullOrEmpty(review.SellerResponse)
                ? new SellerReplyDto
                {
                    Content = review.SellerResponse,
                    CreatedAt = review.SellerResponseDate ?? DateTime.UtcNow
                }
                : null, // Thay đổi structure cho SellerReply
            CreatedAt = review.CreatedAt,
            Status = review.Status.ToString()
        };
    }

    private async Task<OrderDetail> ValidateReviewCreationAsync(Guid userId, CreateReviewDto createDto)
    {
        // 1. Validate user đã mua sản phẩm này
        var orderDetail = await _unitOfWork.OrderDetails
            .FirstOrDefaultAsync(
                od => od.Id == createDto.OrderDetailId && od.Order.UserId == userId,
                od => od.Order,
                od => od.Product,
                od => od.BlindBox,
                od => od.Seller
            );

        if (orderDetail == null)
            throw ErrorHelper.NotFound("Không tìm thấy đơn hàng hoặc bạn không có quyền đánh giá.");

        // 2. Kiểm tra đơn hàng đã hoàn thành chưa
        if (orderDetail.Status != OrderDetailItemStatus.DELIVERED)
            throw ErrorHelper.BadRequest("Chỉ có thể đánh giá sau khi đơn hàng đã được giao.");

        // 3. Check đã review chưa
        var existingReview = await _unitOfWork.Reviews
            .FirstOrDefaultAsync(r => r.OrderDetailId == createDto.OrderDetailId);

        if (existingReview != null)
            throw ErrorHelper.Conflict("Bạn đã đánh giá đơn hàng này rồi.");

        // 4. Validate rating
        if (createDto.OverallRating < 1 || createDto.OverallRating > 5)
            throw ErrorHelper.BadRequest("Đánh giá phải từ 1-5 sao.");

        return orderDetail;
    }

    #endregion
}