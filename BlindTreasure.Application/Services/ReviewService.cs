using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IBlindyService _blindyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoggerService _loggerService;
    private readonly IClaimsService _claimService;
    private readonly IBlobService _blobService;

    public ReviewService(IBlindyService blindyService, IUnitOfWork unitOfWork, ILoggerService loggerService,
        IClaimsService claimService, IBlobService blobService)
    {
        _blindyService = blindyService;
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _claimService = claimService;
        _blobService = blobService;
    }

    public async Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto createDto)
    {
        // 1. Validate input
        if (createDto == null)
            throw ErrorHelper.BadRequest("Dữ liệu đánh giá không hợp lệ");

        if (createDto.ImagesUrls != null && createDto.ImagesUrls.Count > 5)
            throw ErrorHelper.BadRequest("Chỉ được tải lên tối đa 5 hình ảnh");

        // 2. Validate comment content with AI
        if (!string.IsNullOrWhiteSpace(createDto.Comment))
            if (!await _blindyService.ValidateReviewAsync(createDto.Comment))
                throw ErrorHelper.BadRequest("Nội dung đánh giá không phù hợp với tiêu chuẩn cộng đồng");

        var userId = _claimService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy thông tin tài khoản");

        var orderDetail = await _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Include(od => od.Product)
            .ThenInclude(p => p.Category)
            .Include(od => od.BlindBox)
            .FirstOrDefaultAsync(od => od.Id == createDto.OrderDetailId && od.Order.UserId == userId);

        if (orderDetail == null)
            throw ErrorHelper.NotFound("Không tìm thấy chi tiết đơn hàng hoặc đơn hàng không thuộc về bạn");

        if (orderDetail.Order.Status != OrderStatus.PAID.ToString())
            throw ErrorHelper.BadRequest("Chỉ có thể đánh giá sau khi đơn hàng đã được thanh toán thành công");

        var existingReview = await _unitOfWork.Reviews.GetQueryable()
            .FirstOrDefaultAsync(r => r.OrderDetailId == createDto.OrderDetailId && r.UserId == userId && !r.IsDeleted);

        if (existingReview != null)
            throw ErrorHelper.Conflict("Bạn đã đánh giá sản phẩm này trong đơn hàng này rồi");

        // 6. Validate rating
        if (createDto.Rating < 1 || createDto.Rating > 5)
            throw ErrorHelper.BadRequest("Điểm đánh giá phải từ 1 đến 5 sao");

        // 7. Upload images
        var imageUrls = new List<string>();
        if (createDto.ImagesUrls != null && createDto.ImagesUrls.Any())
            foreach (var base64Image in createDto.ImagesUrls)
                try
                {
                    // Validate base64 format
                    if (string.IsNullOrWhiteSpace(base64Image) || !IsValidBase64(base64Image))
                    {
                        _loggerService.Warn("Invalid base64 image format");
                        continue;
                    }

                    var fileName = $"reviews/{userId}/{Guid.NewGuid()}.jpg";
                    var imageBytes = Convert.FromBase64String(base64Image);

                    // Validate image size (max 5MB)
                    if (imageBytes.Length > 5 * 1024 * 1024)
                    {
                        _loggerService.Warn("Image size exceeds 5MB limit");
                        continue;
                    }

                    using var stream = new MemoryStream(imageBytes);
                    await _blobService.UploadFileAsync(fileName, stream);
                    var imageUrl = await _blobService.GetPreviewUrlAsync(fileName);
                    imageUrls.Add(imageUrl);
                }
                catch (Exception ex)
                {
                    _loggerService.Warn($"Failed to upload review image: {ex.Message}");
                }

        // 8. Create review
        var review = new Review
        {
            UserId = userId,
            OrderDetailId = createDto.OrderDetailId,
            ProductId = orderDetail.ProductId,
            BlindBoxId = orderDetail.BlindBoxId,
            SellerId = orderDetail.SellerId,
            OverallRating = createDto.Rating,
            Content = createDto.Comment?.Trim(),
            ImageUrls = imageUrls,
            IsApproved = true, // Auto-approve after AI validation
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Reviews.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        // 10. Return response
        return new ReviewResponseDto
        {
            Id = review.Id,
            UserId = review.UserId,
            UserName = user.FullName,
            UserAvatar = user.AvatarUrl,
            Rating = review.OverallRating,
            Comment = review.Content,
            CreatedAt = review.CreatedAt,
            Category = orderDetail.Product?.Category?.Name ?? "BlindBox",
            Images = review.ImageUrls,
            SellerReply = review.SellerResponseDate.HasValue
                ? new SellerReplyDto
                {
                    Content = review.SellerResponse,
                    CreatedAt = review.SellerResponseDate.Value
                }
                : null
        };
    }

    public async Task<bool> CanReviewOrderDetailAsync(Guid orderDetailId)
    {
        var userId = _claimService.CurrentUserId;

        var orderDetail = await _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .FirstOrDefaultAsync(od => od.Id == orderDetailId && od.Order.UserId == userId);

        if (orderDetail == null)
            return false;

        // Phải là PAID
        if (orderDetail.Order.Status != OrderStatus.PAID.ToString())
            return false;

        // Chưa review
        var existingReview = await _unitOfWork.Reviews.GetQueryable()
            .AnyAsync(r => r.OrderDetailId == orderDetailId && r.UserId == userId && !r.IsDeleted);

        return !existingReview;
    }

    // Helper method to validate base64
    private bool IsValidBase64(string base64String)
    {
        try
        {
            Convert.FromBase64String(base64String);
            return true;
        }
        catch
        {
            return false;
        }
    }
}