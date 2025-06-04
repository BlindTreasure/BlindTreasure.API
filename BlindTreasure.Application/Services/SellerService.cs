using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
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
        IProductService productService)
    {
        _blobService = blobService;
        _emailService = emailService;
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _mapper = mapper;
        _claimsService = claimsService;
        _productService = productService;
    }

    private async Task RemoveSellerCacheAsync(Guid sellerId, Guid userId)
    {
        await _cacheService.RemoveAsync($"seller:{sellerId}");
        await _cacheService.RemoveAsync($"seller:user:{userId}");
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
            _loggerService.Warn($"[UploadSellerDocumentAsync] File không hợp lệ.");
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
            _loggerService.Warn($"[UploadSellerDocumentAsync] Seller {userId} không thể upload ở trạng thái: {seller.Status}");
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
        _loggerService.Info($"[GetAllSellersAsync] Lấy danh sách seller. Page: {pagination.PageIndex}, Size: {pagination.PageSize}");

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

    public async Task<Pagination<ProductDto>> GetAllProductsAsync(ProductQueryParameter param, Guid userId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        _loggerService.Info($"[GetAllProductsAsync] Seller {userId} requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");

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
            query = query.Where(p => p.Status == param.ProductStatus.ToString());

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

        var dtos = items.Select(p => _mapper.Map<Product, ProductDto>(p)).ToList();
        var result = new Pagination<ProductDto>(dtos, count, param.PageIndex, param.PageSize);

        var cacheKey =
            $"product:all:{seller.Id}:{param.PageIndex}:{param.PageSize}:{param.Search}:{param.CategoryId}:{param.ProductStatus}:UpdatedAtDesc";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _loggerService.Info("[GetAllProductsAsync] Product list loaded from DB and cached.");
        return result;
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id, Guid userId)
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
            return _mapper.Map<Product, ProductDto>(cached);
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
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<ProductDto> CreateProductAsync(ProductSellerCreateDto dto)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null)
            throw ErrorHelper.Forbidden("Seller chưa được đăng ký tồn tại.");
        if (!seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        var newProduct = _mapper.Map<ProductSellerCreateDto, ProductCreateDto>(dto);
        newProduct.SellerId = seller.Id;

        var result = await _productService.CreateAsync(newProduct);

        // Xóa cache danh sách sản phẩm của seller để đảm bảo dữ liệu mới nhất
        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}");

        _loggerService.Success($"[CreateProductAsync] Seller {seller.Id} đã tạo sản phẩm mới.");
        return result;
    }

    public async Task<ProductDto> UpdateProductAsync(Guid productId, ProductUpdateDto dto, IFormFile? productImageUrl)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        if (product.SellerId != seller.Id)
            throw ErrorHelper.Forbidden("Bạn chỉ được phép cập nhật sản phẩm của chính mình.");

        var result = await _productService.UpdateAsync(productId, dto, productImageUrl);

        // Xóa cache danh sách sản phẩm của seller để đảm bảo dữ liệu mới nhất
        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}");

        _loggerService.Success($"[UpdateProductAsync] Seller {seller.Id} đã cập nhật sản phẩm {productId}.");
        return result;
    }

    public async Task<ProductDto> DeleteProductAsync(Guid productId)
    {
        var userId = _claimsService.GetCurrentUserId;
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
        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}");

        _loggerService.Success($"[DeleteProductAsync] Seller {seller.Id} đã xóa sản phẩm {productId}.");
        return result;
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