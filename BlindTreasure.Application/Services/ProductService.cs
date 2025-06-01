using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ProductService : IProductService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;


    public ProductService(
        IUnitOfWork unitOfWork,
        ILoggerService logger,
        ICacheService cacheService,
        IClaimsService claimsService,
        IMapperService mapper,
        IBlobService blobService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _claimsService = claimsService;
        _mapper = mapper;
        _blobService = blobService;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var userId = _claimsService.GetCurrentUserId;
        var cacheKey = $"product:{id}";
        var cached = await _cacheService.GetAsync<Product>(cacheKey);
        if (cached != null)
        {
            _logger.Info($"[GetByIdAsync] Cache hit for product {id}");
            if (cached.IsDeleted)
                throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
            if (cached.SellerId != await GetSellerIdByUserId(userId))
                throw ErrorHelper.Forbidden("Không được phép xem sản phẩm của Seller khác.");
            return _mapper.Map<Product, ProductDto>(cached);
        }

        var product = await _unitOfWork.Products.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[GetByIdAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        if (product.SellerId != await GetSellerIdByUserId(userId))
            throw ErrorHelper.Forbidden("Không được phép xem sản phẩm của Seller khác.");

        await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromHours(1));
        _logger.Info($"[GetByIdAsync] Product {id} loaded from DB and cached.");
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<Pagination<ProductDto>> GetAllAsync(ProductQueryParameter param)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);

        if (userId != Guid.Empty || seller != null)
        {
            if (seller == null || !seller.IsVerified)
                throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

            _logger.Info($"[GetAllAsync] Seller {userId} requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");
        }
     

        if (param.PageIndex <= 0 || param.PageSize <= 0)
            throw ErrorHelper.BadRequest("Thông số phân trang không hợp lệ. PageIndex và PageSize phải lớn hơn 0.");

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
        if (!string.IsNullOrWhiteSpace(param.Status))
            query = query.Where(p => p.Status == param.Status);

        // Sort
        query = param.SortBy switch
        {
            ProductSortField.Name => param.Desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            ProductSortField.Price => param.Desc ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            ProductSortField.Stock => param.Desc ? query.OrderByDescending(p => p.Stock) : query.OrderBy(p => p.Stock),
            _ => param.Desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var count = await query.CountAsync();
        if (count == 0)
            _logger.Info("[GetAllAsync] This user dont have any products");

        var items = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var dtos = items.Select(p => _mapper.Map<Product, ProductDto>(p)).ToList();
        var result = new Pagination<ProductDto>(dtos, count, param.PageIndex, param.PageSize);

        var cacheKey = $"product:all:{seller.Id}:{param.PageIndex}:{param.PageSize}:{param.Search}:{param.CategoryId}:{param.Status}:{param.SortBy}:{param.Desc}";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _logger.Info("[GetAllAsync] Product list loaded from DB and cached.");
        return result;
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto, IFormFile? productImageUrl)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified || seller.Status != SellerStatus.Approved)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");
        _logger.Info($"[CreateAsync] Seller {userId} creates product {dto.Name}");

        await ValidateProductDto(dto);

        var product = _mapper.Map<ProductCreateDto, Product>(dto);
        product.ImageUrl = "";
        product.SellerId = seller.Id;
        product.CreatedAt = DateTime.UtcNow;
        product.CreatedBy = userId;

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        if (productImageUrl != null && productImageUrl.Length > 0)
        {
            var imageUrl = await UploadProductImageAsync(product.Id, productImageUrl);

        }

        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}");
        _logger.Success($"[CreateAsync] Product {product.Name} created by seller {userId}");
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductUpdateDto dto)
    {
        var userId = _claimsService.GetCurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[UpdateAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        if (product.SellerId != await GetSellerIdByUserId(userId))
            throw ErrorHelper.Forbidden("Không được phép thao tác sản phẩm của Seller khác.");

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        _logger.Info($"[UpdateAsync] Seller {userId} updates product {product.Name}");

        await ValidateProductDto(dto);

        // Map các trường update
        var updated = _mapper.Map<ProductUpdateDto, Product>(dto);
        product.Name = updated.Name;
        product.Description = updated.Description;
        product.CategoryId = updated.CategoryId;
        product.Price = updated.Price;
        product.Stock = updated.Stock;
        product.Status = updated.Status;
        product.ImageUrl = updated.ImageUrl;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = userId;

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync($"product:{id}");
        await _cacheService.RemoveByPatternAsync($"product:all:{seller.Id}");
        _logger.Success($"[UpdateAsync] Product {id} updated by seller {userId}");
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<ProductDto> DeleteAsync(Guid id)
    {
        var userId = _claimsService.GetCurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[DeleteAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        if (product.SellerId != await GetSellerIdByUserId(userId))
            throw ErrorHelper.Forbidden("Không được phép thao tác sản phẩm của Seller khác.");

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.DeletedBy = userId;

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync($"product:{id}");
        await _cacheService.RemoveByPatternAsync($"product:all:{product.SellerId}");
        _logger.Success($"[DeleteAsync] Product {id} soft deleted by seller {userId}");
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<string?> UploadProductImageAsync(Guid productId, IFormFile file)
    {
        _logger.Info($"[UploadProductImageAsync] Bắt đầu cập nhật avatar cho product {productId}");

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[UploadAvatarAsync] Không tìm thấy user {productId} hoặc đã bị xóa.");
            throw ErrorHelper.NotFound("Người dùng không tồn tại hoặc đã bị xóa.");
        }

        if (file == null || file.Length == 0)
        {
            _logger.Warn("[UploadProductImageAsync] File avatar không hợp lệ.");
            throw ErrorHelper.BadRequest("File ảnh không hợp lệ hoặc rỗng.");
        }

        // Sinh tên file duy nhất để tránh trùng (VD: avatar_userId_timestamp.png)
        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"avatars/avatar_product_{productId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{fileExtension}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetPreviewUrlAsync(fileName);
        if (string.IsNullOrEmpty(fileUrl))
        {
            _logger.Error($"[UploadAvatarAsync] Không thể lấy URL cho file {fileName}");
            throw ErrorHelper.Internal("Không thể tạo URL cho ảnh đại diện.");
        }

        product.ImageUrl = fileUrl;
        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        // Ghi cache theo email và id
        await _cacheService.SetAsync($"product:{product.Id}", product, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{product.Id}", product, TimeSpan.FromHours(1));

        _logger.Success($"[UploadAvatarAsync] Đã cập nhật avatar thành công cho user {product.Id} && {product.Name}");

        return fileUrl;
    }


    #region PRIVATE HELPER METHODS
    private async Task<Guid> GetSellerIdByUserId(Guid userId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.Id == userId);
        var sellerUserId = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.Id == userId);
        if (seller == null && sellerUserId == null)
            throw ErrorHelper.Forbidden("Không tìm thấy seller.");
        return seller.Id;
    }

    private async Task ValidateProductDto(ProductCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw ErrorHelper.BadRequest("Tên sản phẩm không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.Description))
            throw ErrorHelper.BadRequest("Mô tả không được để trống.");
        if (dto.Price <= 0)
            throw ErrorHelper.BadRequest("Giá sản phẩm phải lớn hơn 0.");
        if (dto.Stock < 0)
            throw ErrorHelper.BadRequest("Số lượng tồn kho phải >= 0.");
        var categoryExists = await _unitOfWork.Categories.GetQueryable()
            .AnyAsync(c => c.Id == dto.CategoryId && !c.IsDeleted);
        if (!categoryExists)
            throw ErrorHelper.BadRequest("Danh mục sản phẩm không hợp lệ.");
    }

    private async Task ValidateProductDto(ProductUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw ErrorHelper.BadRequest("Tên sản phẩm không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.Description))
            throw ErrorHelper.BadRequest("Mô tả không được để trống.");
        if (dto.Price <= 0)
            throw ErrorHelper.BadRequest("Giá sản phẩm phải lớn hơn 0.");
        if (dto.Stock < 0)
            throw ErrorHelper.BadRequest("Số lượng tồn kho phải >= 0.");
        var categoryExists = await _unitOfWork.Categories.GetQueryable()
            .AnyAsync(c => c.Id == dto.CategoryId && !c.IsDeleted);
        if (!categoryExists)
            throw ErrorHelper.BadRequest("Danh mục sản phẩm không hợp lệ.");
    }
    #endregion
}