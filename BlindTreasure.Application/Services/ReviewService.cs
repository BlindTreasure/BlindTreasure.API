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
        // Validate input and permissions
        var orderDetail = await ValidateReviewCreationAsync(userId, createDto);

        // AI validate nội dung
        var validation = await _blindyService.ValidateReviewAsync(
            createDto.Comment,
            createDto.OverallRating,
            orderDetail.Seller?.CompanyName,
            orderDetail.Product?.Name ?? orderDetail.BlindBox?.Name
        );

        // Create review
        var review = new Review
        {
            UserId = userId,
            OrderDetailId = createDto.OrderDetailId,
            ProductId = orderDetail.ProductId,
            BlindBoxId = orderDetail.BlindBoxId,
            SellerId = orderDetail.SellerId,
            OverallRating = createDto.OverallRating,
            QualityRating = createDto.QualityRating,
            ServiceRating = createDto.ServiceRating,
            DeliveryRating = createDto.DeliveryRating,
            OriginalComment = createDto.Comment,
            ProcessedComment = validation.CleanedComment ?? createDto.Comment,
            IsCommentValid = validation.IsValid,
            ValidationReason = validation.Reason,
            ImageUrls = createDto.ImageUrls ?? new List<string>(),
            AiValidatedAt = DateTime.UtcNow,
            AiValidationDetails = JsonSerializer.Serialize(validation),
            Status = validation.SuggestedAction switch
            {
                "approve" => ReviewStatus.Approved,
                "moderate" => ReviewStatus.RequiresModeration,
                "reject" => ReviewStatus.Rejected,
                _ => ReviewStatus.RequiresModeration
            },
            IsPublished = validation.SuggestedAction == "approve",
            IsVerifiedPurchase = true
        };

        await _unitOfWork.Reviews.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        // Load lại để có đầy đủ thông tin cho response
        var createdReview = await _unitOfWork.Reviews.GetByIdAsync(review.Id,
            r => r.User,
            r => r.Product,
            r => r.BlindBox,
            r => r.Seller
        );

        return MapReviewToDto(createdReview!);
    }

    public async Task<Pagination<ReviewResponseDto>> GetAllReviewsAsync(ReviewQueryParameter param)
    {
        var baseQuery = _unitOfWork.Reviews.GetQueryable()
            .Include(r => r.User)
            .Include(r => r.Product)
            .Include(r => r.BlindBox)
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

        // Sắp xếp mặc định theo ngày tạo giảm dần
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
            r => r.BlindBox,
            r => r.Seller
        );

        if (review == null)
            throw ErrorHelper.NotFound("Không tìm thấy đánh giá.");

        if (!review.IsPublished)
            throw ErrorHelper.NotFound("Đánh giá không khả dụng.");

        return MapReviewToDto(review);
    }

    #region private methods

    private ReviewResponseDto MapReviewToDto(Review review)
    {
        return new ReviewResponseDto
        {
            Id = review.Id,
            UserName = review.User?.FullName ?? "Ẩn danh",
            UserAvatarUrl = review.User?.AvatarUrl,
            ProductName = review.Product?.Name,
            BlindBoxName = review.BlindBox?.Name,
            SellerName = review.Seller?.CompanyName ?? "Không rõ",
            OverallRating = review.OverallRating,
            QualityRating = review.QualityRating,
            ServiceRating = review.ServiceRating,
            DeliveryRating = review.DeliveryRating,
            Comment = review.ProcessedComment ?? review.OriginalComment,
            ImageUrls = review.ImageUrls ?? new List<string>(),
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            SellerResponse = review.SellerResponse,
            SellerResponseDate = review.SellerResponseDate,
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