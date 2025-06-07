using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
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
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;


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
    ///     API dùng chung cho mọi role: lấy chi tiết sản phẩm theo Id (không ràng buộc seller).
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
            query = query.Where(p => p.Status == param.ProductStatus);
        if (param.SellerId.HasValue)
            query = query.Where(p => p.SellerId == param.SellerId.Value);

        // Sort: UpdatedAt desc, CreatedAt desc
        query = query.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);

        var count = await query.CountAsync();

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
            $"product:all:public:{param.PageIndex}:{param.PageSize}:{param.Search}:{param.CategoryId}:{param.ProductStatus}:{param.SellerId}:UpdatedAtDesc";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _logger.Info("[GetAllAsync] Product list loaded from DB and cached.");
        return result;
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto)
    {
        var userId = _claimsService.CurrentUserId; // cái này chỉ để check là ai đang login, không phải sellerId 
        var seller = await _unitOfWork.Sellers.GetByIdAsync(dto.SellerId);
        if (seller == null)
            throw ErrorHelper.Forbidden("Seller chưa được đăng ký tồn tại.");
        if (!seller.IsVerified || seller.Status != SellerStatus.Approved)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        _logger.Info($"[CreateAsync] Seller {userId} tạo sản phẩm mới: {dto.Name}");

        await ValidateProductDto(dto);

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            CategoryId = dto.CategoryId,
            Price = dto.Price,
            Stock = dto.Stock,
            Height = dto.Height,
            Material = dto.Material,
            ProductType = dto.ProductType,
            Brand = dto.Brand,
            ImageUrls = new List<string>(),
            SellerId = seller.Id,
            Seller = seller,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            IsDeleted = false,
            Status = dto.Status
        };

        var result =
            await _unitOfWork.Products.AddAsync(product); // tracking entity nè bro, savechange xong phải xài thằng này
        await _unitOfWork.SaveChangesAsync();

        if (dto.Images is { Count: > 0 })
        {
            var uploadedUrls = new List<string>();

            foreach (var image in dto.Images.Where(img => img.Length > 0).Take(6))
            {
                var imageUrl = await UploadProductImageAsync(result.Id, image);
                if (!string.IsNullOrEmpty(imageUrl))
                    uploadedUrls.Add(imageUrl);
            }

            if (uploadedUrls.Count > 0)
            {
                // Ghi đè toàn bộ danh sách ảnh, không cộng dồn
                result.ImageUrls = uploadedUrls.Distinct().ToList();
                await _unitOfWork.Products.Update(result);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        await RemoveProductCacheAsync(result.Id, seller.Id);
        _logger.Success($"[CreateAsync] Đã tạo sản phẩm {result.Name} với {result.ImageUrls.Count} ảnh.");

        return _mapper.Map<Product, ProductDto>(result);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductUpdateDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[UpdateAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        _logger.Info($"[UpdateAsync] User {userId} updates product {product.Name}");

        // Chỉ cập nhật trường có giá trị khác null
        if (dto.Name != null)
            product.Name = dto.Name.Trim();
        if (dto.Description != null)
            product.Description = dto.Description.Trim();
        if (dto.CategoryId.HasValue)
            product.CategoryId = dto.CategoryId.Value;
        if (dto.Price.HasValue)
            product.Price = dto.Price.Value;
        if (dto.Stock.HasValue)
            product.Stock = dto.Stock.Value;
        //if (dto.Status.HasValue)
        //    product.Status = dto.Status.Value.ToString();
        if (dto.Height.HasValue)
            product.Height = dto.Height.Value;
        if (dto.Material != null)
            product.Material = dto.Material;
        if (dto.ProductType.HasValue)
            product.ProductType = dto.ProductType.Value;
        if (dto.Brand != null)
            product.Brand = dto.Brand;
        if (dto.ProductStatus.HasValue)
        {
            product.Status = dto.ProductStatus.Value;
        }


        //if (productImageUrl.Length > 0)
        //{
        //    var imageUrl = await UploadProductImageAsync(product.Id, productImageUrl);
        //}


        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        var result = await _unitOfWork.Products.GetByIdAsync(id);

        //await RemoveProductCacheAsync(id, product.SellerId);

        _logger.Success($"[UpdateAsync] Product {id} updated by user {userId}");
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<ProductDto> DeleteAsync(Guid id)
    {
        var userId = _claimsService.CurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[DeleteAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        //if (product.SellerId != await GetSellerIdByUserId(userId))
        //    throw ErrorHelper.Forbidden("Không được phép thao tác sản phẩm của Seller khác.");

        product.Status = ProductStatus.InActive; // Đặt trạng thái là Deleted
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.DeletedBy = userId;

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        await RemoveProductCacheAsync(id, product.SellerId);

        _logger.Success($"[DeleteAsync] Product {id} soft deleted by user {userId}");
        return _mapper.Map<Product, ProductDto>(product);
    }

    public async Task<string?> UploadProductImageAsync(Guid productId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.Warn("[UploadProductImageAsync] File ảnh không hợp lệ hoặc rỗng.");
            throw ErrorHelper.BadRequest("File ảnh không hợp lệ hoặc rỗng.");
        }

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[UploadProductImageAsync] Không tìm thấy sản phẩm {productId} hoặc đã bị xóa.");
            throw ErrorHelper.NotFound("Sản phẩm không tồn tại hoặc đã bị xóa.");
        }

        // Sinh tên file duy nhất bằng Guid để tránh trùng
        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"products/product_thumbnails_{productId}_{Guid.NewGuid():N}{fileExtension}";

        _logger.Info($"[UploadProductImageAsync] Uploading file: {fileName}");

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetPreviewUrlAsync(fileName);
        if (string.IsNullOrEmpty(fileUrl))
        {
            _logger.Error($"[UploadProductImageAsync] Không thể lấy URL cho file {fileName}");
            throw ErrorHelper.Internal("Không thể tạo URL cho ảnh.");
        }

        product.ImageUrls ??= new List<string>();
        product.ImageUrls.Add(fileUrl);

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        // Ghi cache
        await _cacheService.SetAsync($"product:{product.Id}", product, TimeSpan.FromHours(1));

        _logger.Success($"[UploadProductImageAsync] Đã cập nhật image cho product {product.Id}: {fileUrl}");

        return fileUrl;
    }

    public async Task<ProductDto> UpdateProductImagesAsync(Guid productId, List<IFormFile> images)
    {
        var userId = _claimsService.CurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");

        // Xóa ảnh cũ trên MinIO (nếu cần)
        if (product.ImageUrls != null && product.ImageUrls.Count > 0)
        {
            foreach (var url in product.ImageUrls)
            {
                var fileName = ExtractFileNameFromUrl(url);
                if (!string.IsNullOrEmpty(fileName))
                    await _blobService.DeleteFileAsync(fileName);
            }
        }

        var uploadedUrls = new List<string>();
        foreach (var image in images.Where(img => img.Length > 0).Take(6))
        {
            var imageUrl = await UploadProductImageAsync(product.Id, image);
            if (!string.IsNullOrEmpty(imageUrl))
                uploadedUrls.Add(imageUrl);
        }

        product.ImageUrls = uploadedUrls;
        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();
        await RemoveProductCacheAsync(product.Id, product.SellerId);

        return _mapper.Map<Product, ProductDto>(product);
    }

    // Helper để lấy fileName từ URL
    private string ExtractFileNameFromUrl(string url)
    {
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var prefix = query.Get("prefix");
        return prefix != null ? Uri.UnescapeDataString(prefix) : null;
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

    private async Task RemoveProductCacheAsync(Guid productId, Guid sellerId)
    {
        await _cacheService.RemoveAsync($"product:{productId}");
        await _cacheService.RemoveByPatternAsync($"product:all:{sellerId}");
    }

    #endregion
}