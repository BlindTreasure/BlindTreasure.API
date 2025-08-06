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
            throw ErrorHelper.BadRequest();

        if (createDto.ImagesUrls != null && createDto.ImagesUrls.Count > 5)
            throw ErrorHelper.BadRequest("Maximum 5 images allowed");

        // 2. Validate comment content with AI
        if (!await _blindyService.ValidateReviewAsync(createDto.Comment))
            throw ErrorHelper.BadRequest();

        // 3. Get user info
        var userId = _claimService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            throw ErrorHelper.NotFound(ErrorMessages.AccountNotFound);

        // 4. Validate order detail
        var orderDetail = await _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Include(od => od.Product)
            .Include(od => od.BlindBox)
            .FirstOrDefaultAsync(od => od.Id == createDto.OrderDetailId && od.Order.UserId == userId);

        if (orderDetail == null)
            throw ErrorHelper.NotFound();

        if (orderDetail.Status != OrderDetailItemStatus.DELIVERED)
            throw ErrorHelper.BadRequest();

        // 5. Upload images
        var imageUrls = new List<string>();
        if (createDto.ImagesUrls != null && createDto.ImagesUrls.Any())
        {
            foreach (var base64Image in createDto.ImagesUrls)
            {
                try
                {
                    var fileName = $"reviews/{Guid.NewGuid()}.jpg";
                    var imageBytes = Convert.FromBase64String(base64Image);
                    using var stream = new MemoryStream(imageBytes);

                    await _blobService.UploadFileAsync(fileName, stream);
                    var imageUrl = await _blobService.GetPreviewUrlAsync(fileName);
                    imageUrls.Add(imageUrl);
                }
                catch
                {
                    _loggerService.Warn("Failed to upload review image");
                }
            }
        }

        // 6. Create review
        var review = new Review
        {
            UserId = userId,
            OrderDetailId = createDto.OrderDetailId,
            ProductId = orderDetail.ProductId,
            BlindBoxId = orderDetail.BlindBoxId,
            SellerId = orderDetail.SellerId,
            OverallRating = createDto.Rating,
            Content = createDto.Comment,
            ImageUrls = imageUrls,
            IsApproved = true, // Assuming AI validation is sufficient
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Reviews.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        // 7. Return response
        return new ReviewResponseDto
        {
            Id = review.Id,
            UserId = review.UserId,
            UserName = user.FullName,
            UserAvatar = user.AvatarUrl,
            Rating = review.OverallRating,
            Comment = review.Content,
            CreatedAt = review.CreatedAt,
            Category = orderDetail.Product?.Category?.Name,
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
}