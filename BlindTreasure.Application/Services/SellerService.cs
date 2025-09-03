using System.Web;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class SellerService : ISellerService
{
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapper;
    private readonly INotificationService _notificationService;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;

    public SellerService(
        IBlobService blobService,
        IEmailService emailService,
        ILoggerService loggerService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IMapperService mapper,
        IClaimsService claimsService,
        IProductService productService, INotificationService notificationService)
    {
        _blobService = blobService;
        _emailService = emailService;
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _mapper = mapper;
        _claimsService = claimsService;
        _productService = productService;
        _notificationService = notificationService;
    }

    // SellerService: thêm method
    public async Task<List<UserDto>> GetCustomersOfSellerAsync()
    {
        // Lấy seller hiện tại
        var currentUserId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == currentUserId);
        if (seller == null)
            throw ErrorHelper.Forbidden("Không tìm thấy thông tin nhãn hàng.");

        // Lấy danh sách userId distinct từ Orders của seller
        var userIds = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == seller.Id && !o.IsDeleted && o.UserId != Guid.Empty)
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync();

        if (!userIds.Any())
            return new List<UserDto>();

        // Lấy users và map sang UserDto
        var users = await _unitOfWork.Users.GetQueryable()
            .Where(u => userIds.Contains(u.Id) && !u.IsDeleted)
            .ToListAsync();

        var result = users.Select(u => new UserDto
        {
            UserId = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            AvatarUrl = u.AvatarUrl,
            DateOfBirth = u.DateOfBirth,
            Gender = u.Gender,
            Status = u.Status,
            PhoneNumber = u.Phone,
            RoleName = u.RoleName,
            Reason = u.Reason,
            CreatedAt = u.CreatedAt,
            SellerId = u.Seller != null ? (Guid?)u.Seller.Id : null
        }).ToList();

        return result;
    }
    
    public async Task<SellerDto> UpdateSellerInfoAsync(Guid userId, UpdateSellerInfoDto dto)
    {
        _loggerService.Info($"[UpdateSellerInfoAsync] Seller {userId} yêu cầu cập nhật thông tin.");

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId, s => s.User);
        if (seller == null)
        {
            _loggerService.Warn($"[UpdateSellerInfoAsync] Seller {userId} không tồn tại.");
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");
        }

        if (seller.User == null)
        {
            _loggerService.Error($"[UpdateSellerInfoAsync] Seller {userId} không có thông tin user.");
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");
        }

        // Chỉ cập nhật trường có giá trị khác null
        if (!string.IsNullOrWhiteSpace(dto.FullName))
            seller.User.FullName = dto.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            seller.User.Phone = dto.PhoneNumber.Trim();
        if (dto.DateOfBirth.HasValue)
            seller.User.DateOfBirth = dto.DateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            seller.CompanyName = dto.CompanyName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.TaxId))
            seller.TaxId = dto.TaxId.Trim();
        if (!string.IsNullOrWhiteSpace(dto.CompanyAddress))
            seller.CompanyAddress = dto.CompanyAddress.Trim();

        seller.Status = SellerStatus.WaitingReview;


        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        await _notificationService.PushNotificationToUser(
            seller.UserId,
            new NotificationDto
            {
                Title = "Hồ sơ đã gửi",
                Message = "Hồ sơ của bạn đang chờ xét duyệt bởi quản trị viên.",
                Type = NotificationType.System
            }
        );


        // Xóa cache trước khi set lại
        await RemoveSellerCacheAsync(seller.Id, userId);
        await _cacheService.SetAsync($"seller:{seller.Id}", seller, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"seller:user:{userId}", seller, TimeSpan.FromHours(1));

        _loggerService.Success($"[UpdateSellerInfoAsync] Seller {userId} đã cập nhật thông tin thành công.");
        return SellerMapper.ToSellerDto(seller);
    }

    public async Task<string> UploadSellerDocumentAsync(Guid userId, IFormFile file)
    {
        _loggerService.Info($"[UploadSellerDocumentAsync] Seller {userId} upload tài liệu xác minh.");

        if (file == null || file.Length == 0)
        {
            _loggerService.Warn("[UploadSellerDocumentAsync] File không hợp lệ.");
            throw ErrorHelper.BadRequest("File không hợp lệ.");
        }

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null)
        {
            _loggerService.Warn($"[UploadSellerDocumentAsync] Không tìm thấy seller với UserId: {userId}");
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");
        }

        if (seller.Status != SellerStatus.Rejected && seller.Status != SellerStatus.WaitingReview)
        {
            _loggerService.Warn(
                $"[UploadSellerDocumentAsync] Seller {userId} không thể upload ở trạng thái: {seller.Status}");
            throw ErrorHelper.BadRequest("Chỉ seller bị từ chối hoặc chờ duyệt mới được phép nộp lại tài liệu.");
        }

        var fileName = $"seller-documentation/{userId}-{Guid.NewGuid()}_{file.FileName}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetFileUrlAsync(fileName);

        seller.CoaDocumentUrl = fileUrl;
        seller.Status = SellerStatus.WaitingReview;


        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        await _notificationService.PushNotificationToUser(
            seller.UserId,
            new NotificationDto
            {
                Title = "Tài liệu đã nộp",
                Message = "Tài liệu xác minh của bạn đã được gửi và đang chờ xét duyệt.",
                Type = NotificationType.System
            }
        );

        // Cập nhật cache
        await _cacheService.SetAsync($"seller:{seller.Id}", seller, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"seller:user:{userId}", seller, TimeSpan.FromHours(1));

        _loggerService.Success($"[UploadSellerDocumentAsync] Seller {userId} đã upload tài liệu thành công.");
        return fileUrl;
    }

    public async Task<SellerProfileDto> GetSellerProfileByIdAsync(Guid sellerId)
    {
        var cacheKey = $"seller:{sellerId}";
        var cached = await _cacheService.GetAsync<Seller>(cacheKey);
        if (cached != null)
        {
            _loggerService.Info($"[GetSellerProfileByIdAsync] Cache hit for seller {sellerId}");
            return SellerMapper.ToSellerProfileDto(cached);
        }

        var seller = await GetSellerWithUserAsync(sellerId);
        await _cacheService.SetAsync(cacheKey, seller, TimeSpan.FromHours(1));
        _loggerService.Info($"[GetSellerProfileByIdAsync] Seller {sellerId} loaded from DB and cached.");
        return SellerMapper.ToSellerProfileDto(seller);
    }

    public async Task<SellerProfileDto> GetSellerProfileByUserIdAsync(Guid userId)
    {
        var cacheKey = $"seller:user:{userId}";
        var cached = await _cacheService.GetAsync<Seller>(cacheKey);
        if (cached != null)
        {
            _loggerService.Info($"[GetSellerProfileByUserIdAsync] Cache hit for seller user {userId}");
            return SellerMapper.ToSellerProfileDto(cached);
        }

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId, s => s.User);
        if (seller == null)
        {
            _loggerService.Warn($"[GetSellerProfileByUserIdAsync] Seller user {userId} không tồn tại.");
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");
        }

        await _cacheService.SetAsync(cacheKey, seller, TimeSpan.FromHours(1));
        _loggerService.Info($"[GetSellerProfileByUserIdAsync] Seller user {userId} loaded from DB and cached.");
        return SellerMapper.ToSellerProfileDto(seller);
    }

    public async Task<Pagination<SellerDto>> GetAllSellersAsync(SellerStatus? status, PaginationParameter pagination)
    {
        _loggerService.Info(
            $"[GetAllSellersAsync] Lấy danh sách seller. Page: {pagination.PageIndex}, Size: {pagination.PageSize}");

        var query = _unitOfWork.Sellers.GetQueryable()
            .Where(s => !s.IsDeleted)
            .Include(s => s.User)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        // Sort mặc định: UpdatedAt desc, CreatedAt desc
        query = query.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt);

        var totalCount = await query.CountAsync();

        List<Seller> sellers;
        if (pagination.PageIndex == 0)
            sellers = await query.ToListAsync();
        else
            sellers = await query
                .Skip((pagination.PageIndex - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

        var items = sellers.Select(SellerMapper.ToSellerDto).ToList();

        // Không cache toàn bộ danh sách vì có thể rất lớn, chỉ cache từng seller riêng lẻ
        return new Pagination<SellerDto>(items, totalCount, pagination.PageIndex, pagination.PageSize);
    }

    public async Task<Pagination<ProducDetailDto>> GetAllProductsAsync(ProductQueryParameter param, Guid userId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        _loggerService.Info(
            $"[GetAllProductsAsync] Seller {userId} requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Products.GetQueryable()
            .Where(p => !p.IsDeleted && p.SellerId == seller.Id)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(keyword));
        }

        if (param.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == param.CategoryId.Value);
        if (param.ProductStatus.HasValue)
            query = query.Where(p => p.Status == param.ProductStatus);

        query = query.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);

        var count = await query.CountAsync();
        if (count == 0)
            _loggerService.Info("[GetAllProductsAsync] Seller không có sản phẩm nào.");

        List<Product> items;
        if (param.PageIndex == 0)
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(p => _mapper.Map<Product, ProducDetailDto>(p)).ToList();
        var result = new Pagination<ProducDetailDto>(dtos, count, param.PageIndex, param.PageSize);

        var cacheKey =
            $"product:all:{seller.Id}:{param.PageIndex}:{param.PageSize}:{param.Search}:{param.CategoryId}:{param.ProductStatus}:UpdatedAtDesc";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _loggerService.Info("[GetAllProductsAsync] Product list loaded from DB and cached.");
        return result;
    }

    public async Task<ProducDetailDto?> GetProductByIdAsync(Guid id, Guid userId)
    {
        var cacheKey = $"product:{id}";
        var cached = await _cacheService.GetAsync<Product>(cacheKey);
        if (cached != null)
        {
            _loggerService.Info($"[GetProductByIdAsync] Cache hit for product {id}");
            if (cached.IsDeleted)
                throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
            var checkSeller = await GetSellerWithUserAsync(userId);
            if (cached.SellerId != checkSeller.Id)
                throw ErrorHelper.Forbidden("Không được phép xem sản phẩm của Seller khác.");
            return _mapper.Map<Product, ProducDetailDto>(cached);
        }

        var product = await _unitOfWork.Products.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null || product.IsDeleted)
        {
            _loggerService.Warn($"[GetProductByIdAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromHours(1));
        _loggerService.Info($"[GetProductByIdAsync] Product {id} loaded from DB and cached.");
        return _mapper.Map<Product, ProducDetailDto>(product);
    }

    public async Task<ProducDetailDto?> CreateProductAsync(ProductSellerCreateDto dto)
    {
        var userId = _claimsService.CurrentUserId; // chỗ này là lấy user id của seller là người đang login
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId); // seller id ở day86
        if (seller == null)
            throw ErrorHelper.Forbidden("Seller chưa được đăng ký tồn tại.");
        var newProduct = _mapper.Map<ProductSellerCreateDto, ProductCreateDto>(dto);

        newProduct.SellerId = seller.Id; // GÁN SELLER ID VÀO DTO ĐỂ NÉM QUA PRODUCT SERVICE ĐỂ TẠO

        var result = await _productService.CreateAsync(newProduct);

        // Xóa cache danh sách sản phẩm của seller để đảm bảo dữ liệu mới nhất
        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}*");

        _loggerService.Success($"[CreateProductAsync] Seller {seller.Id} đã tạo sản phẩm mới.");
        return result;
    }

    public async Task<ProducDetailDto?> UpdateProductAsync(Guid productId, ProductUpdateDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        if (product.SellerId != seller.Id)
            throw ErrorHelper.Forbidden("Bạn chỉ được phép cập nhật sản phẩm của chính mình.");

        var result = await _productService.UpdateAsync(productId, dto);

        // Xóa cache danh sách sản phẩm của seller để đảm bảo dữ liệu mới nhất
        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}*");

        _loggerService.Success($"[UpdateProductAsync] Seller {seller.Id} đã cập nhật sản phẩm {productId}.");
        return result;
    }

    public async Task<ProducDetailDto> DeleteProductAsync(Guid productId)
    {
        var userId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        if (product.SellerId != seller.Id)
            throw ErrorHelper.Forbidden("Bạn chỉ được phép xóa sản phẩm của chính mình.");

        var result = await _productService.DeleteAsync(productId);

        // Xóa cache danh sách sản phẩm của seller để đảm bảo dữ liệu mới nhất
        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}*");

        _loggerService.Success($"[DeleteProductAsync] Seller {seller.Id} đã xóa sản phẩm {productId}.");
        return result;
    }


    public async Task<ProducDetailDto> UpdateSellerProductImagesAsync(Guid productId, List<IFormFile> images)
    {
        var userId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        if (product.SellerId != seller.Id)
            throw ErrorHelper.Forbidden("Bạn chỉ được phép xóa sản phẩm của chính mình.");


        var result = await _productService.UpdateProductImagesAsync(productId, images);

        return result;
    }

    public async Task<string> UpdateSellerAvatarAsync(Guid userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw ErrorHelper.BadRequest("File không hợp lệ.");

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId, s => s.User);
        if (seller == null || seller.User == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        // Upload file
        var fileName = $"seller-avatars/{userId}-{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);
        var avatarUrl = await _blobService.GetPreviewUrlAsync(fileName);

        // Xóa ảnh cũ nếu có (trừ ảnh mặc định)
        if (!string.IsNullOrEmpty(seller.User.AvatarUrl) && !seller.User.AvatarUrl.Contains("free-psd/3d-illustration"))
            try
            {
                var oldUrl = seller.User.AvatarUrl;
                var uri = new Uri(oldUrl);
                var query = HttpUtility.ParseQueryString(uri.Query);
                var prefix = query.Get("prefix");
                if (!string.IsNullOrEmpty(prefix))
                    await _blobService.DeleteFileAsync(prefix);
            }
            catch
            {
                /* ignore */
            }

        seller.User.AvatarUrl = avatarUrl;
        await _unitOfWork.Users.Update(seller.User);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache
        await _cacheService.RemoveAsync($"seller:{seller.Id}");
        await _cacheService.RemoveAsync($"seller:user:{userId}");

        return avatarUrl;
    }

    public async Task<Pagination<OrderDto>> GetSellerOrdersAsync(OrderQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null)
            throw ErrorHelper.Forbidden("Không tìm thấy seller.");

        var query = _unitOfWork.Orders.GetQueryable()
            .Where(o => o.SellerId == seller.Id && !o.IsDeleted)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Shipments)
            .Include(o => o.OrderDetails).ThenInclude(od => od.BlindBox)
            .Include(o => o.ShippingAddress)
            .Include(o => o.User)
            .Include(o => o.Payment).ThenInclude(p => p.Transactions)
            .AsNoTracking();

        if (param.Status.HasValue)
            query = query.Where(o => o.Status == param.Status.Value.ToString());
        if (param.PlacedFrom.HasValue)
            query = query.Where(o => o.PlacedAt >= param.PlacedFrom.Value);
        if (param.PlacedTo.HasValue)
            query = query.Where(o => o.PlacedAt <= param.PlacedTo.Value);

        query = param.Desc
            ? query.OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            : query.OrderBy(o => o.UpdatedAt ?? o.CreatedAt);

        var totalCount = await query.CountAsync();
        var orders = param.PageIndex == 0
            ? await query.ToListAsync()
            : await query.Skip((param.PageIndex - 1) * param.PageSize).Take(param.PageSize).ToListAsync();

        var dtos = orders.Select(OrderDtoMapper.ToOrderDto).ToList();
        _loggerService.Info("Loaded seller's order list.");
        return new Pagination<OrderDto>(dtos, totalCount, param.PageIndex, param.PageSize);
    }

    public async Task<OrderDto> GetSellerOrderByIdAsync(Guid orderId)
    {
        var userId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null)
            throw ErrorHelper.Forbidden("Không tìm thấy seller tồn tại.");

        var order = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.Id == orderId && o.SellerId == seller.Id && !o.IsDeleted)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Shipments)
            .Include(o => o.OrderDetails).ThenInclude(od => od.BlindBox)
            .Include(o => o.ShippingAddress)
            .Include(o => o.User)
            .Include(o => o.Payment).ThenInclude(p => p.Transactions)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (order == null)
            throw ErrorHelper.NotFound("Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.");

        return OrderDtoMapper.ToOrderDto(order);
    }

    public async Task<SellerOverviewDto?> GetSellerOverviewAsync(Guid sellerId)
    {
        var seller = await _unitOfWork.Sellers.GetQueryable()
            .Include(s => s.BlindBoxes)
            .Include(s => s.Products)
            .Include(s => s.Reviews)
            .FirstOrDefaultAsync(s => s.Id == sellerId && !s.IsDeleted);

        if (seller == null)
            return null;

        // Tính trung bình rating, chỉ lấy review có OverallRating > 0
        var avgRating = seller.Reviews != null && seller.Reviews.Count > 0
            ? seller.Reviews.Where(r => r.OverallRating > 0).Average(r => r.OverallRating)
            : 0;

        var now = DateTime.UtcNow;
        var joinedAt = seller.CreatedAt;
        var joinedAtToText = $"Tham gia từ ngày {joinedAt:MM-dd-yyyy}";

        // Đếm sản phẩm bán trực tiếp hoặc cả hai
        var productInSellingCount = seller.Products?
            .Count(p => p.ProductType == ProductSaleType.DirectSale || p.ProductType == ProductSaleType.Both) ?? 0;

        // Đếm sản phẩm chỉ trong BlindBox hoặc cả hai
        var productInBlindBoxCount = seller.Products?
            .Count(p => p.ProductType == ProductSaleType.BlindBoxOnly || p.ProductType == ProductSaleType.Both) ?? 0;

        // Đếm số lượng BlindBox của seller
        var blindBoxCount = seller.BlindBoxes?.Where(x => x.IsDeleted == false && x.Status == BlindBoxStatus.Approved)
            .Count() ?? 0;

        var dto = new SellerOverviewDto
        {
            SellerId = seller.Id,
            AverageRating = Math.Round(avgRating, 2),
            JoinedAt = seller.CreatedAt,
            ProductCount = seller.Products?.Count ?? 0,
            CompanyName = seller.CompanyName,
            CompanyArea = seller.CompanyProvinceName,
            JoinedAtToText = joinedAtToText,
            ProductInSellingCount = productInSellingCount,
            ProductInBlindBoxCount = productInBlindBoxCount,
            BlindBoxCount = blindBoxCount
        };

        return dto;
    }

    public async Task<SellerSalesStatisticsDto> GetSalesStatisticsAsync(Guid? sellerId = null, DateTime? from = null,
        DateTime? to = null)
    {
        // Lấy sellerId hiện tại nếu không truyền vào
        sellerId ??= (await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == _claimsService.CurrentUserId))?.Id
                     ?? throw ErrorHelper.Forbidden("Không tìm thấy seller.");

        // Lấy các OrderDetail đã bán thành công của seller
        var orderDetailsQuery = _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Where(od => od.Product.SellerId == sellerId
                         && od.Order.Status == OrderStatus.PAID.ToString()
                         && !od.Order.IsDeleted);

        if (from.HasValue)
            orderDetailsQuery = orderDetailsQuery.Where(od => od.Order.PlacedAt >= from.Value);
        if (to.HasValue)
            orderDetailsQuery = orderDetailsQuery.Where(od => od.Order.PlacedAt <= to.Value);

        var orderDetails = await orderDetailsQuery.ToListAsync();

        var totalOrders = orderDetails.Select(od => od.OrderId).Distinct().Count();
        var totalProductsSold = orderDetails.Sum(od => od.Quantity);
        var grossSales = orderDetails.Sum(od => od.TotalPrice);

        // Tính tổng discount từ OrderSellerPromotion
        var orderIds = orderDetails.Select(od => od.OrderId).Distinct().ToList();
        var discounts = await _unitOfWork.OrderSellerPromotions.GetQueryable()
            .Where(osp => osp.SellerId == sellerId && orderIds.Contains(osp.OrderId))
            .SumAsync(osp => osp.DiscountAmount);

        // Tính tổng refund từ Transaction
        var paymentIds = await _unitOfWork.Orders.GetQueryable()
            .Where(o => orderIds.Contains(o.Id) && o.PaymentId != null)
            .Select(o => o.PaymentId.Value)
            .ToListAsync();

        var totalRefunded = await _unitOfWork.Transactions.GetQueryable()
            .Where(t => paymentIds.Contains(t.PaymentId) && t.Type == "Refund")
            .SumAsync(t => t.RefundAmount ?? 0);

        var netSales = grossSales - discounts - totalRefunded;

        return new SellerSalesStatisticsDto
        {
            SellerId = sellerId.Value,
            TotalOrders = totalOrders,
            TotalProductsSold = totalProductsSold,
            GrossSales = grossSales,
            NetSales = netSales,
            TotalRefunded = totalRefunded,
            TotalDiscount = discounts
        };
    }

    private async Task RemoveSellerCacheAsync(Guid sellerId, Guid userId)
    {
        await _cacheService.RemoveAsync($"seller:{sellerId}");
        await _cacheService.RemoveAsync($"seller:user:{userId}");
    }

    // ----------------- PRIVATE HELPER METHODS -----------------

    private async Task<Seller> GetSellerWithUserAsync(Guid sellerId)
    {
        var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId, x => x.User);
        if (seller == null)
        {
            _loggerService.Warn($"[GetSellerWithUserAsync] Seller {sellerId} không tồn tại.");
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");
        }

        if (seller.User == null)
        {
            _loggerService.Error($"[GetSellerWithUserAsync] Seller {sellerId} không có thông tin user.");
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");
        }

        return seller;
    }


    private static void ValidateSellerInfoDto(UpdateSellerInfoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw ErrorHelper.BadRequest("Họ tên không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw ErrorHelper.BadRequest("Số điện thoại không được để trống.");
        if (dto.DateOfBirth == default)
            throw ErrorHelper.BadRequest("Ngày sinh không hợp lệ.");
        if (string.IsNullOrWhiteSpace(dto.CompanyName))
            throw ErrorHelper.BadRequest("Tên công ty không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.TaxId))
            throw ErrorHelper.BadRequest("Mã số thuế không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.CompanyAddress))
            throw ErrorHelper.BadRequest("Địa chỉ công ty không được để trống.");
    }
}