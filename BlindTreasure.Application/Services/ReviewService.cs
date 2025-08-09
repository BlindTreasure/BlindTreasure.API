using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoggerService _loggerService;
    private readonly IClaimsService _claimService;
    private readonly IBlobService _blobService;
    private readonly IUserService _userService;

    public ReviewService(IUnitOfWork unitOfWork, ILoggerService loggerService,
        IClaimsService claimService, IBlobService blobService, IUserService userService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _claimService = claimService;
        _blobService = blobService;
        _userService = userService;
    }

    public async Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto createDto)
    {
        // 1. Validate input using private methods
        ValidateCreateReviewInput(createDto);
        var userId = _claimService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy thông tin tài khoản");

        var orderDetail = await _unitOfWork.OrderDetails
            .GetQueryable()
            .Include(od => od.Order)
            .Include(od => od.Product).ThenInclude(p => p.Seller)
            .Include(od => od.BlindBox)
            .FirstOrDefaultAsync(od => od.Id == createDto.OrderDetailId && od.Order.UserId == userId
            );

        if (orderDetail != null)
        {
            _loggerService.Info(
                $"Found OrderDetail {orderDetail.Id} with Order Status: {orderDetail.Order?.Status}, OrderDetail Status: {orderDetail.Status}");
        }

        await ValidateOrderDetailForReview(orderDetail!, createDto.OrderDetailId, userId);

        // Upload images sử dụng IFormFile
        var imageUrls = await UploadReviewImages(createDto.Images, userId);

        var isContentApproved = true;

        // Create review
        var review = new Review
        {
            UserId = userId,
            OrderDetailId = createDto.OrderDetailId,
            ProductId = orderDetail!.ProductId,
            BlindBoxId = orderDetail.BlindBoxId,
            SellerId = orderDetail.Product.SellerId,
            OverallRating = createDto.Rating,
            Content = createDto.Comment.Trim(),
            ImageUrls = imageUrls,
            IsApproved = isContentApproved,
            ApprovedAt = isContentApproved ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Reviews.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Success(
            $"Review created successfully for OrderDetail {createDto.OrderDetailId} by user {userId}");

        // SỬ DỤNG GetByIdAsync để đảm bảo data nhất quán
        return await GetByIdAsync(review.Id);
    }

    public async Task<ReviewResponseDto> ReplyToReviewAsync(Guid reviewId, string replyContent)
    {
        // Xác thực nội dung phản hồi
        await ValidateReplyContentAsync(replyContent);

        // Xác thực và lấy thông tin người bán
        var userId = await ValidateAndGetSellerIdAsync();

        // Tìm đánh giá cần phản hồi (bỏ điều kiện r.SellerId == sellerId)
        var review = await _unitOfWork.Reviews.GetQueryable()
            .Include(r => r.User)
            .Include(r => r.OrderDetail)
            .ThenInclude(od => od!.Product)
            .ThenInclude(p => p!.Category)
            .Include(r => r.OrderDetail)
            .ThenInclude(od => od!.BlindBox)
            .Include(r => r.Seller)
            .Where(r => !r.IsDeleted && r.Id == reviewId)
            .FirstOrDefaultAsync();

        if (review == null)
            throw ErrorHelper.NotFound("Không tìm thấy đánh giá");

        // Tìm Seller entity dựa trên UserId
        var seller = await _unitOfWork.Sellers.GetQueryable()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy thông tin người bán");

        // Kiểm tra xem sản phẩm hoặc blindbox trong review có thuộc về người bán không
        var hasPermission = false;

        if (review.ProductId.HasValue)
            hasPermission = await _unitOfWork.Products.GetQueryable()
                .AnyAsync(p => p.Id == review.ProductId && p.SellerId == seller.Id);

        if (!hasPermission && review.BlindBoxId.HasValue)
            hasPermission = await _unitOfWork.BlindBoxes.GetQueryable()
                .AnyAsync(b => b.Id == review.BlindBoxId && b.SellerId == seller.Id);

        if (!hasPermission)
            throw ErrorHelper.Forbidden("Bạn không có quyền phản hồi đánh giá này");

        // Cập nhật phản hồi
        review.SellerResponse = replyContent.Trim();
        review.SellerResponseDate = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        review.UpdatedBy = userId;

        await _unitOfWork.Reviews.Update(review);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Success($"Seller {seller.Id} (User {userId}) replied to review {reviewId} successfully");

        return await GetByIdAsync(review.Id);
    }

    public async Task<Pagination<ReviewResponseDto>> GetAllReviewsAsync(ReviewQueryParameter param)
    {
        if (param == null)
            throw ErrorHelper.BadRequest("Tham số truy vấn không hợp lệ");

        // Validate các tham số đầu vào
        ValidateReviewQueryParameters(param);

        _loggerService.Info(
            $"Fetching reviews with parameters: ProductId={param.ProductId}, BlindBoxId={param.BlindBoxId}, SellerId={param.SellerId}");

        // Xây dựng truy vấn cơ bản
        var query = _unitOfWork.Reviews.GetQueryable()
            .Include(r => r.User)
            .Include(r => r.OrderDetail)
            .ThenInclude(od => od!.Product)
            .ThenInclude(p => p!.Category)
            .Include(r => r.OrderDetail)
            .ThenInclude(od => od!.BlindBox)
            .Include(r => r.Seller)
            .Where(r => !r.IsDeleted && r.IsApproved);

        // Áp dụng các bộ lọc và sắp xếp
        query = ApplyFiltersAndSorting(query, param);

        // Đếm tổng số bản ghi trước khi phân trang
        var count = await query.CountAsync();
        _loggerService.Info($"Total matching reviews: {count}");

        // Áp dụng phân trang
        var items = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        _loggerService.Info($"Retrieved {items.Count} reviews for page {param.PageIndex}");

        // Chuyển đổi sang DTO và trả về kết quả
        var dtos = items.Select(MapToReviewResponseDto).ToList();
        var result = new Pagination<ReviewResponseDto>(dtos, count, param.PageIndex, param.PageSize);

        return result;
    }

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

    public async Task<bool> HasReviewedOrderDetailAsync(Guid orderDetailId)
    {
        var userId = _claimService.CurrentUserId;

        // Kiểm tra có review nào với orderDetailId này không
        var reviewsWithOrderDetail = await _unitOfWork.Reviews.GetQueryable()
            .Where(r => r.OrderDetailId == orderDetailId)
            .ToListAsync();

        Console.WriteLine($"Reviews with OrderDetailId {orderDetailId}: {reviewsWithOrderDetail.Count}");

        // Kiểm tra có review nào của user hiện tại không
        var reviewsWithUser = await _unitOfWork.Reviews.GetQueryable()
            .Where(r => r.OrderDetailId == orderDetailId && r.UserId == userId)
            .ToListAsync();

        Console.WriteLine($"Reviews with UserId {userId}: {reviewsWithUser.Count}");

        // Kiểm tra có review nào chưa bị xóa không
        var activeReviews = await _unitOfWork.Reviews.GetQueryable()
            .Where(r => r.OrderDetailId == orderDetailId && r.UserId == userId && !r.IsDeleted)
            .ToListAsync();

        Console.WriteLine($"Active reviews: {activeReviews.Count}");

        return activeReviews.Any();
    }

    public async Task<ReviewResponseDto> DeleteReviewAsync(Guid reviewId)
    {
        // Lấy bài đánh giá bằng id
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

        //Lấy userId bằng claim
        var userId = _claimService.CurrentUserId;

        //Kiểm tra nếu người dùng chưa đăng nhập
        if (userId == Guid.Empty)
        {
            _loggerService.Error("No user ID found in claims");
            throw ErrorHelper.Unauthorized("Bạn cần đăng nhập để thực hiện hành động này");
        }

        //Kiểm tra bài đánh giá có tồn tại không
        if (review == null)
            throw ErrorHelper.NotFound("Không tìm thấy đánh giá");

        //Kiểm tra bài đánh giá có thuộc về tài khoản đang đăng nhập hay không
        if (review.UserId != userId)
            throw ErrorHelper.BadRequest("Đây không phải là bài đánh giá của bạn, không thực hiện được hành động này.");

        review.IsDeleted = true;

        await _unitOfWork.Reviews.Update(review);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Success($"User {userId} delete the review {reviewId} successfully");

        return MapToReviewResponseDto(review);
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
    /// Kiểm tra tính hợp lệ của các tham số truy vấn đánh giá
    /// </summary>
    private void ValidateReviewQueryParameters(ReviewQueryParameter param)
    {
        // Không thể lọc đồng thời theo cả Product và BlindBox
        if (param.ProductId.HasValue && param.BlindBoxId.HasValue)
        {
            _loggerService.Warn("Cannot filter by both ProductId and BlindBoxId simultaneously");
            throw ErrorHelper.BadRequest("Không thể lọc đồng thời theo cả sản phẩm và hộp quà bí mật");
        }

        // Kiểm tra giá trị Rating hợp lệ
        if (param.MinRating.HasValue && (param.MinRating.Value < 1 || param.MinRating.Value > 5))
        {
            _loggerService.Warn($"Invalid MinRating value: {param.MinRating.Value}");
            throw ErrorHelper.BadRequest("Giá trị đánh giá tối thiểu phải từ 1 đến 5");
        }

        if (param.MaxRating.HasValue && (param.MaxRating.Value < 1 || param.MaxRating.Value > 5))
        {
            _loggerService.Warn($"Invalid MaxRating value: {param.MaxRating.Value}");
            throw ErrorHelper.BadRequest("Giá trị đánh giá tối đa phải từ 1 đến 5");
        }

        // Kiểm tra MinRating <= MaxRating
        if (param.MinRating.HasValue && param.MaxRating.HasValue && param.MinRating.Value > param.MaxRating.Value)
        {
            _loggerService.Warn(
                $"MinRating ({param.MinRating.Value}) is greater than MaxRating ({param.MaxRating.Value})");
            throw ErrorHelper.BadRequest("Giá trị đánh giá tối thiểu không thể lớn hơn giá trị đánh giá tối đa");
        }

        // Kiểm tra giá trị phân trang
        if (param.PageIndex < 1)
        {
            _loggerService.Warn($"Invalid PageIndex value: {param.PageIndex}");
            throw ErrorHelper.BadRequest("Số trang phải lớn hơn 0");
        }

        if (param.PageSize < 1 || param.PageSize > 100)
        {
            _loggerService.Warn($"Invalid PageSize value: {param.PageSize}");
            throw ErrorHelper.BadRequest("Kích thước trang phải từ 1 đến 100");
        }

        _loggerService.Info("Review query parameters validation passed");
    }

    /// <summary>
    /// Áp dụng các bộ lọc và sắp xếp cho truy vấn đánh giá
    /// </summary>
    private IQueryable<Review> ApplyFiltersAndSorting(IQueryable<Review> query, ReviewQueryParameter param)
    {
        // Lọc theo sản phẩm hoặc blindbox nếu có
        if (param.ProductId.HasValue)
        {
            query = query.Where(r => r.ProductId == param.ProductId.Value);
            _loggerService.Info($"Filtering by ProductId: {param.ProductId.Value}");
        }

        if (param.BlindBoxId.HasValue)
        {
            query = query.Where(r => r.BlindBoxId == param.BlindBoxId.Value);
            _loggerService.Info($"Filtering by BlindBoxId: {param.BlindBoxId.Value}");
        }

        // Lọc theo seller nếu có
        if (param.SellerId.HasValue)
        {
            query = query.Where(r => r.SellerId == param.SellerId.Value);
            _loggerService.Info($"Filtering by SellerId: {param.SellerId.Value}");
        }

        // Lọc theo rating nếu có
        if (param.MinRating.HasValue)
        {
            query = query.Where(r => r.OverallRating >= param.MinRating.Value);
            _loggerService.Info($"Filtering by MinRating: {param.MinRating.Value}");
        }

        if (param.MaxRating.HasValue)
        {
            query = query.Where(r => r.OverallRating <= param.MaxRating.Value);
            _loggerService.Info($"Filtering by MaxRating: {param.MaxRating.Value}");
        }

        // Lọc theo có comment hay không
        if (param.HasComment.HasValue)
        {
            query = param.HasComment.Value
                ? query.Where(r => !string.IsNullOrWhiteSpace(r.Content))
                : query.Where(r => string.IsNullOrWhiteSpace(r.Content));
            _loggerService.Info($"Filtering by HasComment: {param.HasComment.Value}");
        }

        // Lọc theo có hình ảnh hay không
        if (param.HasImages.HasValue)
        {
            query = param.HasImages.Value
                ? query.Where(r => r.ImageUrls.Count > 0)
                : query.Where(r => r.ImageUrls.Count == 0);
            _loggerService.Info($"Filtering by HasImages: {param.HasImages.Value}");
        }

        if (param.HasSellerReply.HasValue)
        {
            query = param.HasSellerReply.Value
                ? query.Where(r => !string.IsNullOrWhiteSpace(r.SellerResponse) && r.SellerResponseDate.HasValue)
                : query.Where(r => string.IsNullOrWhiteSpace(r.SellerResponse) || !r.SellerResponseDate.HasValue);
            _loggerService.Info($"Filtering by HasSellerReply: {param.HasSellerReply.Value}");
        }

        query = param.SortBy?.ToLower() switch
        {
            "rating_asc" => query.OrderBy(r => r.OverallRating),
            "rating_desc" => query.OrderByDescending(r => r.OverallRating),
            "newest" => query.OrderByDescending(r => r.CreatedAt),
            "oldest" => query.OrderBy(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt) // Mặc định sắp xếp theo thời gian tạo mới nhất
        };

        _loggerService.Info($"Applied sorting: {param.SortBy ?? "newest (default)"}");

        return query;
    }

    /// <summary>
    /// Xác thực nội dung phản hồi của người bán
    /// </summary>
    private async Task ValidateReplyContentAsync(string replyContent)
    {
        if (string.IsNullOrWhiteSpace(replyContent))
        {
            _loggerService.Warn("Empty reply content in seller response");
            throw ErrorHelper.BadRequest("Nội dung phản hồi không được để trống");
        }

        if (replyContent.Length > 1000)
        {
            _loggerService.Warn($"Reply content too long: {replyContent.Length} characters");
            throw ErrorHelper.BadRequest("Nội dung phản hồi không được vượt quá 1000 ký tự");
        }
    }

    /// <summary>
    /// Xác thực và lấy ID của người bán hiện tại
    /// </summary>
    private async Task<Guid> ValidateAndGetSellerIdAsync()
    {
        try
        {
            // Lấy ID người dùng từ claims
            var userId = _claimService.CurrentUserId;

            if (userId == Guid.Empty)
            {
                _loggerService.Error("No user ID found in claims");
                throw ErrorHelper.Unauthorized("Bạn cần đăng nhập để thực hiện hành động này");
            }

            // Truy vấn thông tin user để kiểm tra vai trò và trạng thái
            try
            {
                var user = await _userService.GetUserById(userId);

                if (user == null)
                {
                    _loggerService.Error($"User with ID {userId} not found or deleted");
                    throw ErrorHelper.NotFound("Không tìm thấy thông tin người dùng");
                }

                // Kiểm tra role thủ công
                if (user.RoleName != RoleType.Seller)
                {
                    _loggerService.Error($"User with ID {userId} has role {user.RoleName}, not Seller");
                    throw ErrorHelper.Forbidden("Bạn không có quyền thực hiện hành động này");
                }

                if (user.Status != UserStatus.Active)
                {
                    _loggerService.Warn($"User with ID {userId} has status {user.Status}");
                    throw ErrorHelper.Forbidden("Tài khoản đang không ở trạng thái hoạt động");
                }

                _loggerService.Info($"Successfully validated user {userId} with Seller role");
                return userId;
            }
            catch (Exception dbEx) when (!(dbEx is ApplicationException))
            {
                _loggerService.Error($"Database error when fetching user: {dbEx.Message}");
                throw ErrorHelper.Internal("Lỗi khi truy vấn thông tin người dùng");
            }
        }
        catch (Exception ex) when
            (!(ex is ApplicationException)) // Bắt tất cả ngoại lệ không phải do ErrorHelper tạo ra
        {
            _loggerService.Error($"Unexpected error validating seller role: {ex.Message}");
            throw ErrorHelper.Internal("Đã xảy ra lỗi khi xác thực quyền người bán");
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
                foreach (var imageFile in createDto.Images)
                    if (!IsValidMediaFile(imageFile))
                    {
                        _loggerService.Warn($"Invalid image file: {imageFile.FileName}");
                        throw ErrorHelper.BadRequest($"File {imageFile.FileName} không hợp lệ");
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
    /// Validate OrderDetail for review eligibility - Enhanced with detailed logging
    /// </summary>
    private async Task ValidateOrderDetailForReview(OrderDetail orderDetail, Guid orderDetailId, Guid userId)
    {
        _loggerService.Info("=== START ValidateOrderDetailForReview ===");
        _loggerService.Info($"Input Parameters:");
        _loggerService.Info($"- OrderDetailId: {orderDetailId}");
        _loggerService.Info($"- Current UserId: {userId}");
        _loggerService.Info($"- OrderDetail is null: {orderDetail == null}");

        if (orderDetail == null)
        {
            _loggerService.Error($"❌ OrderDetail not found for ID: {orderDetailId}");
            _loggerService.Error("Possible reasons:");
            _loggerService.Error("1. OrderDetailId không tồn tại trong database");
            _loggerService.Error("2. Include() không đủ navigation properties");
            _loggerService.Error("3. Query condition bị sai");
            throw ErrorHelper.NotFound("Không tìm thấy chi tiết đơn hàng hoặc đơn hàng không thuộc về bạn");
        }

        try
        {
            _loggerService.Info("=== OrderDetail Information ===");
            _loggerService.Info($"- OrderDetail.Id: {orderDetail.Id}");
            _loggerService.Info($"- OrderDetail.OrderId: {orderDetail.OrderId}");
            _loggerService.Info($"- OrderDetail.CreatedBy: {orderDetail.CreatedBy}");
            _loggerService.Info($"- OrderDetail.CreatedAt: {orderDetail.CreatedAt}");
            _loggerService.Info($"- OrderDetail.Status: {orderDetail.Status}");
            _loggerService.Info($"- OrderDetail.ProductId: {orderDetail.ProductId}");
            _loggerService.Info($"- OrderDetail.BlindBoxId: {orderDetail.BlindBoxId}");
            _loggerService.Info($"- OrderDetail.Quantity: {orderDetail.Quantity}");
            _loggerService.Info($"- OrderDetail.UnitPrice: {orderDetail.UnitPrice}");

            // Kiểm tra Order có null không
            if (orderDetail.Order == null)
            {
                _loggerService.Error("❌ Order is null for OrderDetail");
                _loggerService.Error("Possible reasons:");
                _loggerService.Error("1. Missing .Include(od => od.Order) trong query");
                _loggerService.Error("2. Order bị soft delete (IsDeleted = true)");
                _loggerService.Error("3. Foreign key relationship bị broken");
                throw ErrorHelper.BadRequest("Thông tin đơn hàng không hợp lệ");
            }

            _loggerService.Info("=== Order Information ===");
            _loggerService.Info($"- Order.Id: {orderDetail.Order.Id}");
            _loggerService.Info($"- Order.UserId: {orderDetail.Order.UserId}");
            _loggerService.Info($"- Order.SellerId: {orderDetail.Order.SellerId}");
            _loggerService.Info($"- Order.Status: {orderDetail.Order.Status}");
            _loggerService.Info($"- Order.CreatedBy: {orderDetail.Order.CreatedBy}");
            _loggerService.Info($"- Order.CreatedAt: {orderDetail.Order.CreatedAt}");
            _loggerService.Info($"- Order.PlacedAt: {orderDetail.Order.PlacedAt}");
            _loggerService.Info($"- Order.IsDeleted: {orderDetail.Order.IsDeleted}");
            _loggerService.Info($"- Order.TotalAmount: {orderDetail.Order.TotalAmount}");
            _loggerService.Info($"- Order.FinalAmount: {orderDetail.Order.FinalAmount}");

            // ===== KIỂM TRA OWNERSHIP =====
            _loggerService.Info("=== Ownership Validation ===");
            _loggerService.Info($"- Order.UserId: {orderDetail.Order.UserId}");
            _loggerService.Info($"- Current UserId: {userId}");
            _loggerService.Info($"- UserId Match: {orderDetail.Order.UserId == userId}");
            _loggerService.Info($"- UserId Type: {orderDetail.Order.UserId.GetType().Name}");
            _loggerService.Info($"- Current UserId Type: {userId.GetType().Name}");

            if (orderDetail.Order.UserId != userId)
            {
                _loggerService.Error("❌ Order ownership validation FAILED");
                _loggerService.Error($"Expected UserId: {userId}");
                _loggerService.Error($"Actual Order.UserId: {orderDetail.Order.UserId}");
                _loggerService.Error("Possible reasons:");
                _loggerService.Error("1. User đang cố gắng access order của người khác");
                _loggerService.Error("2. JWT token bị sai hoặc expired");
                _loggerService.Error("3. ClaimsService.CurrentUserId trả về sai giá trị");
                _loggerService.Error("4. Database có vấn đề về Order.UserId");
                throw ErrorHelper.NotFound("Đơn hàng không thuộc về bạn");
            }

            _loggerService.Info("✅ Order ownership validation PASSED");

            // ===== KIỂM TRA ORDER STATUS =====
            _loggerService.Info("=== Order Status Validation ===");
            _loggerService.Info($"- Current Order Status: '{orderDetail.Order.Status}'");
            _loggerService.Info($"- Expected Status: '{nameof(OrderStatus.PAID)}'");
            _loggerService.Info($"- Status Match: {orderDetail.Order.Status == nameof(OrderStatus.PAID)}");
            _loggerService.Info($"- Order Status Enum Values: [{string.Join(", ", Enum.GetNames<OrderStatus>())}]");

            if (orderDetail.Order.Status != nameof(OrderStatus.PAID))
            {
                _loggerService.Error("❌ Order status validation FAILED");
                _loggerService.Error($"Current Status: '{orderDetail.Order.Status}'");
                _loggerService.Error($"Required Status: '{nameof(OrderStatus.PAID)}'");
                _loggerService.Error("Possible reasons:");
                _loggerService.Error("1. Order chưa được thanh toán (PENDING)");
                _loggerService.Error("2. Order bị hủy (CANCELLED)");
                _loggerService.Error("3. Order đã hết hạn (EXPIRED)");
                _loggerService.Error("4. Enum conversion có vấn đề");
                throw ErrorHelper.BadRequest("Chỉ có thể đánh giá sau khi đơn hàng đã được thanh toán thành công");
            }

            _loggerService.Info("✅ Order status validation PASSED");

            // ===== KIỂM TRA DUPLICATE REVIEW =====
            _loggerService.Info("=== Duplicate Review Check ===");
            _loggerService.Info($"Checking existing reviews for:");
            _loggerService.Info($"- OrderDetailId: {orderDetailId}");
            _loggerService.Info($"- UserId: {userId}");

            var existingReview = await _unitOfWork.Reviews.GetQueryable()
                .Where(r => r.OrderDetailId == orderDetailId && r.UserId == userId && !r.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingReview != null)
            {
                _loggerService.Error("❌ Duplicate review validation FAILED");
                _loggerService.Error($"Existing Review Details:");
                _loggerService.Error($"- Review.Id: {existingReview.Id}");
                _loggerService.Error($"- Review.CreatedAt: {existingReview.CreatedAt}");
                _loggerService.Error($"- Review.Rating: {existingReview.OverallRating}");
                _loggerService.Error(
                    $"- Review.Content: {existingReview.Content?.Substring(0, Math.Min(50, existingReview.Content?.Length ?? 0))}...");
                _loggerService.Error($"- Review.IsDeleted: {existingReview.IsDeleted}");
                throw ErrorHelper.Conflict("Bạn đã đánh giá sản phẩm này trong đơn hàng này rồi");
            }

            _loggerService.Info("✅ Duplicate review check PASSED - No existing review found");

            // ===== OPTIONAL: LOG ADDITIONAL DETAILS =====
            _loggerService.Info("=== Additional Information ===");
            if (orderDetail.Product != null)
            {
                _loggerService.Info($"- Product.Id: {orderDetail.Product.Id}");
                _loggerService.Info($"- Product.Name: {orderDetail.Product.Name}");
                _loggerService.Info($"- Product.SellerId: {orderDetail.Product.SellerId}");
                _loggerService.Info($"- Product.Status: {orderDetail.Product.Status}");
            }

            if (orderDetail.BlindBox != null)
            {
                _loggerService.Info($"- BlindBox.Id: {orderDetail.BlindBox.Id}");
                _loggerService.Info($"- BlindBox.Name: {orderDetail.BlindBox.Name}");
                _loggerService.Info($"- BlindBox.SellerId: {orderDetail.BlindBox.SellerId}");
                _loggerService.Info($"- BlindBox.Status: {orderDetail.BlindBox.Status}");
            }

            _loggerService.Info("✅ OrderDetail validation completed successfully");
            _loggerService.Info($"User {userId} is eligible to review OrderDetail {orderDetailId}");
            _loggerService.Info("=== END ValidateOrderDetailForReview ===");
        }
        catch (ApplicationException) // ErrorHelper exceptions
        {
            _loggerService.Error("🚫 Validation failed with application exception - re-throwing");
            throw;
        }
        catch (Exception ex)
        {
            _loggerService.Error("💥 Unexpected error during OrderDetail validation");
            _loggerService.Error($"Exception Type: {ex.GetType().Name}");
            _loggerService.Error($"Exception Message: {ex.Message}");
            _loggerService.Error($"Inner Exception: {ex.InnerException?.Message}");
            _loggerService.Error($"Stack Trace: {ex.StackTrace}");
            _loggerService.Error($"OrderDetail Debug Info:");
            _loggerService.Error($"- OrderDetailId: {orderDetailId}");
            _loggerService.Error($"- OrderId: {orderDetail?.OrderId}");
            _loggerService.Error($"- Order Status: {orderDetail?.Order?.Status}");
            _loggerService.Error($"- Order UserId: {orderDetail?.Order?.UserId}");
            _loggerService.Error($"- Current UserId: {userId}");
            _loggerService.Error($"- OrderDetail Status: {orderDetail?.Status}");

            throw ErrorHelper.Internal("Đã xảy ra lỗi khi kiểm tra thông tin đơn hàng. Vui lòng thử lại sau.");
        }
    }

    private bool IsValidMediaFile(IFormFile file)
    {
        try
        {
            // Check if file is null or empty
            if (file.Length == 0)
            {
                _loggerService.Warn("Empty file detected");
                return false;
            }

            // Check file size (max 50MB)
            const int maxSizeBytes = 50 * 1024 * 1024; // 50MB
            if (file.Length > maxSizeBytes)
            {
                _loggerService.Warn(
                    $"File {file.FileName} exceeds size limit: {file.Length} bytes (max: {maxSizeBytes})");
                return false;
            }

            // Check file extension
            var allowedExtensions = new[]
            {
                // Images
                ".jpg", ".jpeg", ".png", ".gif", ".webp",
                // Videos
                ".mp4", ".mov", ".avi", ".wmv", ".mkv"
            };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                _loggerService.Warn($"File {file.FileName} has invalid extension: {fileExtension}");
                return false;
            }

            // Check MIME type
            var allowedMimeTypes = new[]
            {
                // Images
                "image/jpeg",
                "image/jpg",
                "image/png",
                "image/gif",
                "image/webp",
                // Videos
                "video/mp4",
                "video/quicktime",
                "video/x-msvideo",
                "video/x-ms-wmv",
                "video/x-matroska"
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
            _loggerService.Error($"Error validating media file {file?.FileName}: {ex.Message}");
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
            }

        _loggerService.Info(
            $"Image upload summary: {successCount} success, {failCount} failed out of {images.Count} total");

        return imageUrls;
    }

    #endregion
}