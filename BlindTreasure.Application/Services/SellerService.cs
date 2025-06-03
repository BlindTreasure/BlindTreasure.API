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
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IMapperService _mapper;
    private readonly IClaimsService _claimsService;
    private readonly IProductService _productService;

    public SellerService(IBlobService blobService, IEmailService emailService, ILoggerService loggerService, IUnitOfWork unitOfWork, ICacheService cacheService, IMapperService mapper, IClaimsService claimsService, IProductService productService)
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

    public async Task<SellerDto> UpdateSellerInfoAsync(Guid userId, UpdateSellerInfoDto dto)
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

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId, s => s.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        if (seller.User == null)
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");

        // Cập nhật User
        seller.User.FullName = dto.FullName;
        seller.User.Phone = dto.PhoneNumber;
        seller.User.DateOfBirth = dto.DateOfBirth;

        // Cập nhật Seller
        seller.CompanyName = dto.CompanyName;
        seller.TaxId = dto.TaxId;
        seller.CompanyAddress = dto.CompanyAddress;
        seller.Status = SellerStatus.WaitingReview;

        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Info($"[UpdateSellerInfoAsync] Seller {userId} đã cập nhật thông tin.");

        return SellerMapper.ToSellerDto(seller);
    }

    public async Task<string> UploadSellerDocumentAsync(Guid userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _loggerService.Error($"[UploadSellerDocumentAsync] User {userId} upload thất bại: file không hợp lệ.");
            throw ErrorHelper.BadRequest("File không hợp lệ.");
        }

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null)
        {
            _loggerService.Error($"[UploadSellerDocumentAsync] Không tìm thấy hồ sơ seller với UserId: {userId}");
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");
        }

        if (seller.Status != SellerStatus.Rejected && seller.Status != SellerStatus.WaitingReview)
        {
            _loggerService.Error(
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

        _loggerService.Info($"[UploadSellerDocumentAsync] Seller {userId} re-submitted COA document: {fileName}");

        return fileUrl;
    }


    public async Task<SellerProfileDto> GetSellerProfileByIdAsync(Guid sellerId)
    {
        var seller = await GetSellerWithUserAsync(sellerId);
        return SellerMapper.ToSellerProfileDto(seller);
    }

    public async Task<SellerProfileDto> GetSellerProfileByUserIdAsync(Guid userId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId, s => s.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        return SellerMapper.ToSellerProfileDto(seller);
    }


    public async Task<Pagination<SellerDto>> GetAllSellersAsync(SellerStatus? status, PaginationParameter pagination)
    {
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
        {
            sellers = await query.ToListAsync();
        }
        else
        {
            sellers = await query
                .Skip((pagination.PageIndex - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();
        }

        var items = sellers.Select(SellerMapper.ToSellerDto).ToList();

        return new Pagination<SellerDto>(items, totalCount, pagination.PageIndex, pagination.PageSize);
    }

    public async Task<Pagination<ProductDto>> GetAllProductsAsync(ProductQueryParameter param,  Guid userId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        _loggerService.Info($"[GetAllAsync] Seller {userId} requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Products.GetQueryable()
            .Where(p => !p.IsDeleted && p.SellerId == seller.Id)
            .AsNoTracking();

        // Filter
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(keyword));
        }
        if (param.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == param.CategoryId.Value);
        if (param.ProductStatus.HasValue)
            query = query.Where(p => p.Status == param.ProductStatus.ToString());

        // Sort: UpdatedAt desc, CreatedAt desc
        query = query.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);

        var count = await query.CountAsync();
        if (count == 0)
            _loggerService.Info("[GetAllAsync] This user don't have any products");

        List<Product> items;
        if (param.PageIndex == 0)
        {
            items = await query.ToListAsync();
        }
        else
        {
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();
        }

        var dtos = items.Select(p => _mapper.Map<Product, ProductDto>(p)).ToList();
        var result = new Pagination<ProductDto>(dtos, count, param.PageIndex, param.PageSize);

        var cacheKey = $"product:all:{seller.Id}:{param.PageIndex}:{param.PageSize}:{param.Search}:{param.CategoryId}:{param.ProductStatus}:UpdatedAtDesc";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _loggerService.Info("[GetAllAsync] Product list loaded from DB and cached.");
        return result;
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id, Guid userId)
    {
        var cacheKey = $"product:{id}";
        var cached = await _cacheService.GetAsync<Product>(cacheKey);
        if (cached != null)
        {
            _loggerService.Info($"[GetByIdAsync] Cache hit for product {id}");
            if (cached.IsDeleted)
                throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
            var checkSeller = await GetSellerWithUserAsync(userId);
            if (cached.SellerId != checkSeller.Id )
                throw ErrorHelper.Forbidden("Không được phép xem sản phẩm của Seller khác.");
            return _mapper.Map<Product, ProductDto>(cached);
        }

        var product = await _unitOfWork.Products.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null || product.IsDeleted)
        {
            _loggerService.Warn($"[GetByIdAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

       

        await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromHours(1));
        _loggerService.Info($"[GetByIdAsync] Product {id} loaded from DB and cached.");
        return _mapper.Map<Product, ProductDto>(product);
    }

    /// <summary>
    /// Seller tạo sản phẩm mới (chỉ cho phép tạo sản phẩm cho chính mình).
    /// </summary>
    public async Task<ProductDto> CreateProductAsync(ProductSellerCreateDto dto, IFormFile? productImageUrl)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null )
        {
            throw ErrorHelper.Forbidden("Seller chưa được đăng ký tồn tại.");    
        }

        if (!seller.IsVerified)
        {
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");
        }



        var newProduct = _mapper.Map<ProductSellerCreateDto, ProductCreateDto>(dto);
        newProduct.SellerId = seller.Id; // Gán SellerId từ seller hiện tại

        // Gọi ProductService để tạo sản phẩm
        return await _productService.CreateAsync(newProduct, productImageUrl);
    }

    /// <summary>
    /// Seller cập nhật sản phẩm (chỉ cho phép cập nhật sản phẩm của chính mình).
    /// </summary>
    public async Task<ProductDto> UpdateProductAsync(Guid productId, ProductUpdateDto dto, IFormFile? productImageUrl)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        // Kiểm tra sản phẩm có thuộc seller này không
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        if (product.SellerId != seller.Id)
            throw ErrorHelper.Forbidden("Bạn chỉ được phép cập nhật sản phẩm của chính mình.");

        // Gọi ProductService để cập nhật sản phẩm
        return await _productService.UpdateAsync(productId, dto, productImageUrl);
    }

    /// <summary>
    /// Seller xóa mềm sản phẩm (chỉ cho phép xóa sản phẩm của chính mình).
    /// </summary>
    public async Task<ProductDto> DeleteProductAsync(Guid productId)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        // Kiểm tra sản phẩm có thuộc seller này không
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        if (product.SellerId != seller.Id)
            throw ErrorHelper.Forbidden("Bạn chỉ được phép xóa sản phẩm của chính mình.");

        // Gọi ProductService để xóa sản phẩm
        return await _productService.DeleteAsync(productId);
    }



    //private method

    private async Task<Seller> GetSellerWithUserAsync(Guid sellerId)
    {
        var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId, x => x.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        if (seller.User == null)
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");

        return seller;
    }

    
}