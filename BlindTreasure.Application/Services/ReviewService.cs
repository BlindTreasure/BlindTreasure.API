using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
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
        // 1. Validate input using private methods
        ValidateCreateReviewInput(createDto);

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
            .ThenInclude(p => p!.Category)
            .Include(od => od.BlindBox)
            .FirstOrDefaultAsync(od => od.Id == createDto.OrderDetailId && od.Order.UserId == userId);

        await ValidateOrderDetailForReview(orderDetail!, createDto.OrderDetailId, userId);

        // Upload images sử dụng IFormFile
        var imageUrls = await UploadReviewImages(createDto.Images, userId);

        // Create review
        var review = new Review
        {
            UserId = userId,
            OrderDetailId = createDto.OrderDetailId,
            ProductId = orderDetail!.ProductId,
            BlindBoxId = orderDetail.BlindBoxId,
            SellerId = orderDetail.SellerId,
            OverallRating = createDto.Rating,
            Content = createDto.Comment.Trim(),
            ImageUrls = imageUrls,
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Reviews.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Success(
            $"Review created successfully for OrderDetail {createDto.OrderDetailId} by user {userId}");

        // SỬ DỤNG GetByIdAsync để đảm bảo data nhất quán
        return await GetByIdAsync(review.Id);
    }

    /// <summary>
    /// Lấy review theo ID với đầy đủ thông tin liên quan
    /// </summary>
    public async Task<ReviewResponseDto> GetByIdAsync(Guid reviewId)
    {
        var review = await _unitOfWork.Reviews.GetQueryable()
            .Include(r => r.User)
            .Include(r => r.OrderDetail)
            .ThenInclude(od => od!.Product)
            .ThenInclude(p => p!.Category)
            .Include(r => r.OrderDetail)
            .ThenInclude(od => od!.BlindBox)
            .Include(r => r.Seller)
            .Where(r => !r.IsDeleted)
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review == null)
            throw ErrorHelper.NotFound("Không tìm thấy đánh giá");

        return MapToReviewResponseDto(review);
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
        if (orderDetail.Order.Status != nameof(OrderStatus.PAID))
            return false;

        // Chưa review
        var existingReview = await _unitOfWork.Reviews.GetQueryable()
            .AnyAsync(r => r.OrderDetailId == orderDetailId && r.UserId == userId && !r.IsDeleted);

        return !existingReview;
    }

    #region private methods

    /// <summary>
    /// CORE METHOD: Map Review entity sang ReviewResponseDto
    /// Tất cả methods khác sẽ dùng method này để đảm bảo data mapping nhất quán
    /// </summary>
    private ReviewResponseDto MapToReviewResponseDto(Review review)
    {
        if (review == null)
        {
            _loggerService.Error("Review entity is null in MapToReviewResponseDto");
            throw new ArgumentNullException(nameof(review));
        }

        try
        {
            // Determine category name
            var categoryName = review.OrderDetail?.Product?.Category?.Name ?? "BlindBox";
            _loggerService.Info($"Determined category name: {categoryName}");

            // Determine item name for logging/display
            var itemName = review.OrderDetail?.Product?.Name ?? review.OrderDetail?.BlindBox?.Name ?? "Unknown Item";
            _loggerService.Info($"Determined item name: {itemName}");

            var responseDto = new ReviewResponseDto
            {
                Id = review.Id,
                UserId = review.UserId,
                UserName = review.User?.FullName ?? "Anonymous User",
                UserAvatar = review.User?.AvatarUrl,
                Rating = review.OverallRating,
                Comment = review.Content,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt,
                Category = categoryName,
                ItemName = itemName,
                Images = review.ImageUrls ?? new List<string>(),
                IsApproved = review.IsApproved,
                ApprovedAt = review.ApprovedAt,
                SellerReply = review.SellerResponseDate.HasValue
                    ? new SellerReplyDto
                    {
                        Content = review.SellerResponse ?? string.Empty,
                        CreatedAt = review.SellerResponseDate.Value,
                        SellerName = review.Seller?.CompanyName ?? "Seller"
                    }
                    : null,
                OrderDetailId = review.OrderDetailId,
                ProductId = review.ProductId,
                BlindBoxId = review.BlindBoxId,
                SellerId = review.SellerId
            };

            _loggerService.Info($"Successfully mapped review {review.Id} to ReviewResponseDto");
            return responseDto;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"Failed to map review {review.Id} to ReviewResponseDto: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validate basic input data for CreateReviewDto
    /// </summary>
    private void ValidateCreateReviewInput(CreateReviewDto createDto)
    {
        if (createDto == null)
        {
            _loggerService.Error("CreateReviewDto is null in ValidateCreateReviewInput");
            throw ErrorHelper.BadRequest("Dữ liệu đánh giá không hợp lệ");
        }

        try
        {
            // Validate OrderDetailId
            if (createDto.OrderDetailId == Guid.Empty)
            {
                _loggerService.Warn("Empty OrderDetailId in CreateReviewDto");
                throw ErrorHelper.BadRequest("ID chi tiết đơn hàng là bắt buộc");
            }

            // Validate Rating
            if (createDto.Rating < 1 || createDto.Rating > 5)
            {
                _loggerService.Warn($"Invalid rating value: {createDto.Rating}");
                throw ErrorHelper.BadRequest("Điểm đánh giá phải từ 1 đến 5 sao");
            }

            // Validate Comment
            if (string.IsNullOrWhiteSpace(createDto.Comment))
            {
                _loggerService.Warn("Empty comment in CreateReviewDto");
                throw ErrorHelper.BadRequest("Nội dung đánh giá là bắt buộc");
            }

            if (createDto.Comment.Length < 10)
            {
                _loggerService.Warn($"Comment too short: {createDto.Comment.Length} characters");
                throw ErrorHelper.BadRequest("Nội dung đánh giá phải có ít nhất 10 ký tự");
            }

            if (createDto.Comment.Length > 2000)
            {
                _loggerService.Warn($"Comment too long: {createDto.Comment.Length} characters");
                throw ErrorHelper.BadRequest("Nội dung đánh giá không được vượt quá 2000 ký tự");
            }

            // Validate Images
            if (createDto.Images != null && createDto.Images.Count > 5)
            {
                _loggerService.Warn($"Too many images: {createDto.Images.Count} files");
                throw ErrorHelper.BadRequest("Chỉ được tải lên tối đa 5 hình ảnh");
            }

            // Validate each image file
            if (createDto.Images != null && createDto.Images.Any())
            {
                foreach (var imageFile in createDto.Images)
                {
                    if (!IsValidImageFile(imageFile))
                    {
                        _loggerService.Warn($"Invalid image file: {imageFile.FileName}");
                        throw ErrorHelper.BadRequest($"File {imageFile.FileName} không hợp lệ");
                    }
                }
            }

            _loggerService.Info("Successfully validated CreateReviewDto input");
        }
        catch (Exception ex)
        {
            _loggerService.Error($"Error validating CreateReviewDto: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validate OrderDetail for review eligibility
    /// </summary>
    private async Task ValidateOrderDetailForReview(OrderDetail orderDetail, Guid orderDetailId, Guid userId)
    {
        if (orderDetail == null)
        {
            _loggerService.Error($"OrderDetail not found for ID: {orderDetailId}");
            throw ErrorHelper.NotFound("Không tìm thấy chi tiết đơn hàng hoặc đơn hàng không thuộc về bạn");
        }

        try
        {
            // Check order status
            if (orderDetail.Order.Status != nameof(OrderStatus.PAID))
            {
                _loggerService.Warn(
                    $"Order {orderDetail.OrderId} has invalid status for review: {orderDetail.Order.Status}");
                throw ErrorHelper.BadRequest("Chỉ có thể đánh giá sau khi đơn hàng đã được thanh toán thành công");
            }

            // Check if already reviewed
            var existingReview = await _unitOfWork.Reviews.GetQueryable()
                .FirstOrDefaultAsync(r => r.OrderDetailId == orderDetailId && r.UserId == userId && !r.IsDeleted);

            if (existingReview != null)
            {
                _loggerService.Warn($"Duplicate review attempt for OrderDetail {orderDetailId} by User {userId}");
                throw ErrorHelper.Conflict("Bạn đã đánh giá sản phẩm này trong đơn hàng này rồi");
            }

            _loggerService.Info($"Successfully validated OrderDetail {orderDetailId} for review eligibility");
        }
        catch (Exception ex)
        {
            _loggerService.Error($"Error validating OrderDetail {orderDetailId} for review: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validate individual image file
    /// </summary>
    private bool IsValidImageFile(IFormFile file)
    {
        try
        {
            // Check if file is null or empty
            if (file.Length == 0)
            {
                _loggerService.Warn("Empty file detected");
                return false;
            }

            // Check file size (max 5MB)
            const int maxSizeBytes = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxSizeBytes)
            {
                _loggerService.Warn(
                    $"File {file.FileName} exceeds size limit: {file.Length} bytes (max: {maxSizeBytes})");
                return false;
            }

            // Check file extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                _loggerService.Warn($"File {file.FileName} has invalid extension: {fileExtension}");
                return false;
            }

            // Check MIME type
            var allowedMimeTypes = new[]
            {
                "image/jpeg",
                "image/jpg",
                "image/png",
                "image/gif",
                "image/webp"
            };

            if (string.IsNullOrEmpty(file.ContentType) ||
                !allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                _loggerService.Warn($"File {file.FileName} has invalid MIME type: {file.ContentType}");
                return false;
            }

            _loggerService.Info($"File {file.FileName} passed validation");
            return true;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"Error validating image file {file?.FileName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Upload review images and return list of URLs
    /// </summary>
    private async Task<List<string>> UploadReviewImages(List<IFormFile>? images, Guid userId)
    {
        var imageUrls = new List<string>();

        if (images == null || !images.Any())
        {
            _loggerService.Info("No images to upload");
            return imageUrls;
        }

        var successCount = 0;
        var failCount = 0;

        _loggerService.Info($"Starting upload of {images.Count} images for user {userId}");

        foreach (var imageFile in images)
        {
            try
            {
                // Generate unique filename
                var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                var fileName = $"reviews/{userId}/{Guid.NewGuid()}{fileExtension}";

                _loggerService.Info($"Uploading image {fileName}");

                // Upload file to MinIO via BlobService
                using var stream = imageFile.OpenReadStream();
                await _blobService.UploadFileAsync(fileName, stream);

                // Get public URL
                var imageUrl = await _blobService.GetPreviewUrlAsync(fileName);
                imageUrls.Add(imageUrl);
                successCount++;

                _loggerService.Info($"Successfully uploaded review image: {fileName}");
            }
            catch (Exception ex)
            {
                failCount++;
                _loggerService.Error($"Failed to upload review image {imageFile.FileName}: {ex.Message}");
                // Continue with other images even if one fails
            }
        }

        _loggerService.Info(
            $"Image upload summary: {successCount} success, {failCount} failed out of {images.Count} total");

        return imageUrls;
    }

    #endregion
}