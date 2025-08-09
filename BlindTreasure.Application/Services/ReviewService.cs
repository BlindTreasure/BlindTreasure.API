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
        try
        {
            // 1. Validate input using private methods
            ValidateCreateReviewInput(createDto);

            var userId = _claimService.CurrentUserId;
            if (userId == Guid.Empty)
            {
                _loggerService.Error("CurrentUserId is empty in CreateReviewAsync");
                throw ErrorHelper.Unauthorized("Bạn cần đăng nhập để thực hiện hành động này");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
            {
                _loggerService.Error($"User not found or deleted: {userId}");
                throw ErrorHelper.NotFound("Không tìm thấy thông tin tài khoản");
            }

            // 2. Tìm OrderDetail với error handling tốt hơn
            OrderDetail? orderDetail = null;
            try
            {
                orderDetail = await _unitOfWork.OrderDetails
                    .GetQueryable()
                    .Include(od => od.Order)
                    .Include(od => od.Product)
                    .ThenInclude(p => p != null ? p.Seller : null)
                    .Include(od => od.BlindBox)
                    .ThenInclude(b => b != null ? b.Seller : null)
                    .FirstOrDefaultAsync(od => od.Id == createDto.OrderDetailId
                                               && od.Order != null
                                               && od.Order.UserId == userId
                                               && !od.IsDeleted);
            }
            catch (Exception ex)
            {
                _loggerService.Error(
                    $"Database error when fetching OrderDetail {createDto.OrderDetailId}: {ex.Message}");
                throw ErrorHelper.Internal("Lỗi khi truy vấn thông tin đơn hàng");
            }

            if (orderDetail?.Order == null)
            {
                _loggerService.Error($"OrderDetail not found or Order is null: {createDto.OrderDetailId}");
                throw ErrorHelper.NotFound("Không tìm thấy chi tiết đơn hàng hoặc đơn hàng không thuộc về bạn");
            }

            _loggerService.Info($"Found OrderDetail {orderDetail.Id} with Order Status: {orderDetail.Order.Status}");

            // 3. Validate OrderDetail với try-catch
            try
            {
                await ValidateOrderDetailForReview(orderDetail, createDto.OrderDetailId, userId);
            }
            catch (ApplicationException)
            {
                // Re-throw application exceptions (từ ErrorHelper)
                throw;
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Unexpected error in ValidateOrderDetailForReview: {ex.Message}");
                throw ErrorHelper.Internal("Lỗi khi kiểm tra tính hợp lệ của đơn hàng");
            }

            // 4. Upload images với error handling
            List<string> imageUrls = new List<string>();
            try
            {
                imageUrls = await UploadReviewImages(createDto.Images, userId);
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Error uploading review images: {ex.Message}");
                // Không throw exception cho upload ảnh, chỉ log và tiếp tục
                imageUrls = new List<string>();
            }

            // 5. Xác định SellerId an toàn
            Guid? sellerId = null;
            try
            {
                if (orderDetail.Product?.Seller != null)
                {
                    sellerId = orderDetail.Product.Seller.Id;
                }
                else if (orderDetail.BlindBox?.Seller != null)
                {
                    sellerId = orderDetail.BlindBox.Seller.Id;
                }
                else
                {
                    // Fallback: tìm seller từ database
                    if (orderDetail.ProductId.HasValue)
                    {
                        var product = await _unitOfWork.Products
                            .GetQueryable()
                            .Include(p => p.Seller)
                            .FirstOrDefaultAsync(p => p.Id == orderDetail.ProductId);
                        sellerId = product?.SellerId;
                    }
                    else if (orderDetail.BlindBoxId.HasValue)
                    {
                        var blindBox = await _unitOfWork.BlindBoxes
                            .GetQueryable()
                            .Include(b => b.Seller)
                            .FirstOrDefaultAsync(b => b.Id == orderDetail.BlindBoxId);
                        sellerId = blindBox?.SellerId;
                    }
                }

                if (!sellerId.HasValue)
                {
                    _loggerService.Warn($"Could not determine SellerId for OrderDetail {orderDetail.Id}");
                    // Không throw exception, chỉ để null
                }
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Error determining SellerId: {ex.Message}");
                // Không throw, để sellerId = null
            }

            var isContentApproved = true;

            // 6. Create review với error handling
            Review review;
            try
            {
                review = new Review
                {
                    UserId = userId,
                    OrderDetailId = createDto.OrderDetailId,
                    ProductId = orderDetail.ProductId,
                    BlindBoxId = orderDetail.BlindBoxId,
                    SellerId = sellerId, // Có thể null
                    OverallRating = createDto.Rating,
                    Content = createDto.Comment?.Trim() ?? string.Empty,
                    ImageUrls = imageUrls ?? new List<string>(),
                    IsApproved = isContentApproved,
                    ApprovedAt = isContentApproved ? DateTime.UtcNow : null,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Reviews.AddAsync(review);
                await _unitOfWork.SaveChangesAsync();

                _loggerService.Success(
                    $"Review created successfully for OrderDetail {createDto.OrderDetailId} by user {userId}");
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Database error when creating review: {ex.Message}");
                throw ErrorHelper.Internal("Lỗi khi lưu đánh giá vào cơ sở dữ liệu");
            }

            // 7. Return result với error handling
            try
            {
                return await GetByIdAsync(review.Id);
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Error getting created review {review.Id}: {ex.Message}");
                // Fallback: tạo response đơn giản
                return new ReviewResponseDto
                {
                    Id = review.Id,
                    UserId = review.UserId,
                    UserName = user.FullName ?? "Unknown User",
                    Rating = review.OverallRating,
                    Comment = review.Content,
                    CreatedAt = review.CreatedAt,
                    Category = "Unknown",
                    ItemName = "Unknown Item",
                    Images = review.ImageUrls ?? new List<string>(),
                    IsApproved = review.IsApproved,
                    ApprovedAt = review.ApprovedAt,
                    OrderDetailId = review.OrderDetailId,
                    ProductId = review.ProductId,
                    BlindBoxId = review.BlindBoxId,
                    SellerId = review.SellerId
                };
            }
        }
        catch (ApplicationException)
        {
            // Re-throw application exceptions (từ ErrorHelper)
            throw;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"Unexpected error in CreateReviewAsync: {ex.Message}");
            _loggerService.Error($"Stack trace: {ex.StackTrace}");
            throw ErrorHelper.Internal("Đã xảy ra lỗi không mong muốn khi tạo đánh giá");
        }
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
        try
        {
            _loggerService.Info("=== START ValidateOrderDetailForReview ===");

            if (orderDetail?.Order == null)
            {
                _loggerService.Error($"OrderDetail or Order is null for ID: {orderDetailId}");
                throw ErrorHelper.NotFound("Không tìm thấy chi tiết đơn hàng hoặc đơn hàng không thuộc về bạn");
            }

            // Kiểm tra ownership
            if (orderDetail.Order.UserId != userId)
            {
                _loggerService.Error(
                    $"Order ownership validation failed. Expected: {userId}, Actual: {orderDetail.Order.UserId}");
                throw ErrorHelper.Forbidden("Đơn hàng không thuộc về bạn");
            }

            // Kiểm tra order status - thêm null check
            var orderStatus = orderDetail.Order.Status;
            if (string.IsNullOrEmpty(orderStatus))
            {
                _loggerService.Error("Order status is null or empty");
                throw ErrorHelper.BadRequest("Trạng thái đơn hàng không hợp lệ");
            }

            if (orderStatus != nameof(OrderStatus.PAID))
            {
                _loggerService.Error($"Invalid order status: {orderStatus}. Required: {nameof(OrderStatus.PAID)}");
                throw ErrorHelper.BadRequest("Chỉ có thể đánh giá sau khi đơn hàng đã được thanh toán thành công");
            }

            // Kiểm tra duplicate review với error handling
            try
            {
                var existingReview = await _unitOfWork.Reviews.GetQueryable()
                    .Where(r => r.OrderDetailId == orderDetailId && r.UserId == userId && !r.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingReview != null)
                {
                    _loggerService.Error($"Duplicate review found: {existingReview.Id}");
                    throw ErrorHelper.Conflict("Bạn đã đánh giá sản phẩm này trong đơn hàng này rồi");
                }
            }
            catch (ApplicationException)
            {
                throw; // Re-throw ErrorHelper exceptions
            }
            catch (Exception ex)
            {
                _loggerService.Error($"Database error checking duplicate review: {ex.Message}");
                throw ErrorHelper.Internal("Lỗi khi kiểm tra đánh giá trùng lặp");
            }

            _loggerService.Info("✅ OrderDetail validation completed successfully");
        }
        catch (ApplicationException)
        {
            throw; // Re-throw ErrorHelper exceptions
        }
        catch (Exception ex)
        {
            _loggerService.Error($"Unexpected error in ValidateOrderDetailForReview: {ex.Message}");
            throw ErrorHelper.Internal("Đã xảy ra lỗi khi kiểm tra thông tin đơn hàng");
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

        try
        {
            if (images == null || !images.Any())
            {
                _loggerService.Info("No images to upload");
                return imageUrls;
            }

            _loggerService.Info($"Starting upload of {images.Count} images for user {userId}");

            foreach (var imageFile in images)
            {
                try
                {
                    if (imageFile == null || imageFile.Length == 0)
                    {
                        _loggerService.Warn("Skipping null or empty image file");
                        continue;
                    }

                    // Generate unique filename
                    var fileExtension = Path.GetExtension(imageFile.FileName)?.ToLowerInvariant() ?? ".jpg";
                    var fileName = $"reviews/{userId}/{Guid.NewGuid()}{fileExtension}";

                    _loggerService.Info($"Uploading image {fileName}");

                    // Upload file to MinIO via BlobService
                    using var stream = imageFile.OpenReadStream();
                    await _blobService.UploadFileAsync(fileName, stream);

                    // Get public URL
                    var imageUrl = await _blobService.GetPreviewUrlAsync(fileName);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        imageUrls.Add(imageUrl);
                        _loggerService.Info($"Successfully uploaded review image: {fileName}");
                    }
                    else
                    {
                        _loggerService.Warn($"Failed to get URL for uploaded image: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    _loggerService.Error($"Failed to upload individual image {imageFile?.FileName}: {ex.Message}");
                    // Tiếp tục với ảnh tiếp theo, không throw exception
                }
            }

            _loggerService.Info(
                $"Upload completed. Successfully uploaded {imageUrls.Count} out of {images.Count} images");
            return imageUrls;
        }
        catch (Exception ex)
        {
            _loggerService.Error($"Unexpected error in UploadReviewImages: {ex.Message}");
            // Trả về danh sách rỗng thay vì throw exception
            return new List<string>();
        }
    }

    #endregion
}