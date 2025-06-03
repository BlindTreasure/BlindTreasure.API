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

    /// <summary>
    /// API dùng chung cho mọi role: lấy chi tiết sản phẩm theo Id (không ràng buộc seller).
    /// </summary>
    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var cacheKey = $"product:{id}";
        var cached = await _cacheService.GetAsync<Product>(cacheKey);
        if (cached != null)
        {
            _logger.Info($"[GetByIdAsync] Cache hit for product {id}");
            if (cached.IsDeleted)
                throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
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

        await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromHours(1));
        _logger.Info($"[GetByIdAsync] Product {id} loaded from DB and cached.");
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<Pagination<ProductDto>> GetAllAsync(ProductQueryParameter param)
    {
        _logger.Info($"[GetAllAsync] Public requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Products.GetQueryable()
            .Where(p => !p.IsDeleted)
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
        if (param.SellerId.HasValue)
            query = query.Where(p => p.SellerId == param.SellerId.Value);

        // Sort: UpdatedAt desc, CreatedAt desc
        query = query.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);

        var count = await query.CountAsync();

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

        var cacheKey = $"product:all:public:{param.PageIndex}:{param.PageSize}:{param.Search}:{param.CategoryId}:{param.ProductStatus}:{param.SellerId}:UpdatedAtDesc";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _logger.Info("[GetAllAsync] Product list loaded from DB and cached.");
        return result;
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto, IFormFile? productImageUrl)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.GetByIdAsync(dto.SellerId, x=> x.User);
        if (seller == null || !seller.IsVerified || seller.Status != SellerStatus.Approved)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");
        _logger.Info($"[CreateAsync] Seller {userId} creates product {dto.Name}");

        await ValidateProductDto(dto);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            IsDeleted = false,
            Seller = seller
        };
        product.Name = dto.Name.Trim();
        product.Description = dto.Description.Trim();
        product.CategoryId = dto.CategoryId;
        product.Price = dto.Price;
        product.Stock = dto.Stock;
        product.Height = dto.Height;
        product.Material = dto.Material;
        product.ProductType = dto.ProductType;
        product.Brand = dto.Brand;
        // Mặc định ảnh sản phẩm là rỗng khi tạo mới

        product.ImageUrl = "";
        product.SellerId = seller.Id;
        product.CreatedAt = DateTime.UtcNow;
        product.CreatedBy = userId;
        product.Status= dto.Status.ToString() ; // Mặc định là Active khi tạo mới

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

    public async Task<ProductDto> UpdateAsync(Guid id, ProductUpdateDto dto, IFormFile productImageUrl)
    {
        var userId = _claimsService.GetCurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[UpdateAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        //if (product.SellerId != await GetSellerIdByUserId(userId))
        //    throw ErrorHelper.Forbidden("Không được phép thao tác sản phẩm của Seller khác.");

        //var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        //if (seller == null || !seller.IsVerified)
        //    throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        _logger.Info($"[UpdateAsync] Seller {userId} updates product {product.Name}");

        await ValidateProductDto(dto);

        // Map các trường update
        
        product.Name = dto.Name;
        product.Description = dto.Description;
        product.CategoryId = dto.CategoryId;
        product.Price = dto.Price;
        product.Stock = dto.Stock;
        product.Status = dto.Status.ToString();
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = userId;
        product.Height = dto.Height;
        product.Material = dto.Material;
        product.ProductType = dto.ProductType;
        product.Brand = dto.Brand;


        if (productImageUrl != null && productImageUrl.Length > 0)
        {
            var imageUrl = await UploadProductImageAsync(product.Id, productImageUrl);

        }

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync($"product:{id}");
        await _cacheService.RemoveByPatternAsync($"product:all:{userId}");
        _logger.Success($"[UpdateAsync] Product {id} updated by user {userId}");
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

        //if (product.SellerId != await GetSellerIdByUserId(userId))
        //    throw ErrorHelper.Forbidden("Không được phép thao tác sản phẩm của Seller khác.");

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.DeletedBy = userId;

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync($"product:{id}");
        await _cacheService.RemoveByPatternAsync($"product:all:{product.SellerId}");
        _logger.Success($"[DeleteAsync] Product {id} soft deleted by user {userId}");
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

        _logger.Success($"[UploadAvatarAsync] Đã cập nhật image thành công cho product {product.Id} tên {product.Name}");

        return fileUrl;
    }


    #region PRIVATE HELPER METHODS

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