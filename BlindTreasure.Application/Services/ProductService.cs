using System.Web;
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
    private readonly ICategoryService _categoryService;
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
        IBlobService blobService, ICategoryService categoryService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _claimsService = claimsService;
        _mapper = mapper;
        _blobService = blobService;
        _categoryService = categoryService;
    }

    /// <summary>
    ///     API dùng chung cho mọi role: lấy chi tiết sản phẩm theo Id (không ràng buộc seller).
    /// </summary>
    public async Task<ProducDetailstDto> GetByIdAsync(Guid id)
    {
        var cacheKey = $"product:{id}";
        var cached = await _cacheService.GetAsync<Product>(cacheKey);
        if (cached != null)
        {
            _logger.Info($"[GetByIdAsync] Cache hit for product {id}");
            if (cached.IsDeleted)
                throw ErrorHelper.NotFound(ErrorMessages.ProductNotFound);
            return MapProductToDetailDto(cached);
        }

        var product = await _unitOfWork.Products.GetQueryable()
            .Include(p => p.Seller)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);


        if (product == null || product.IsDeleted)
        {
            _logger.Warn($"[GetByIdAsync] Product {id} not found or deleted.");
            throw ErrorHelper.NotFound(ErrorMessages.ProductNotFound);
        }

        await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromHours(1));
        _logger.Info($"[GetByIdAsync] Product {id} loaded from DB and cached.");
        return MapProductToDetailDto(product);
    }

    public async Task<Pagination<ProducDetailstDto>> GetAllAsync(ProductQueryParameter param)
    {
        _logger.Info($"[GetAllAsync] Public requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Products.GetQueryable()
            .Where(p => !p.IsDeleted && p.ProductType == ProductSaleType.DirectSale)
            .AsNoTracking();

        // Filter
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(keyword));
        }

        if (param.CategoryId.HasValue)
        {
            var categoryIds = await _categoryService.GetAllChildCategoryIdsAsync(param.CategoryId.Value);
            query = query.Where(p => categoryIds.Contains(p.CategoryId));
        }

        if (param.ProductStatus.HasValue)
            query = query.Where(p => p.Status == param.ProductStatus);

        if (param.SellerId.HasValue)
            query = query.Where(p => p.SellerId == param.SellerId.Value);

        if (param.MinPrice.HasValue)
            query = query.Where(p => p.Price >= param.MinPrice.Value);

        if (param.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= param.MaxPrice.Value);

        if (param.ReleaseDateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= param.ReleaseDateFrom.Value);

        if (param.ReleaseDateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= param.ReleaseDateTo.Value);

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

        var dtos = items.Select(MapProductToDetailDto).ToList();
        var result = new Pagination<ProducDetailstDto>(dtos, count, param.PageIndex, param.PageSize);

        return result;
    }

    public async Task<ProducDetailstDto> CreateAsync(ProductCreateDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.GetByIdAsync(dto.SellerId);
        if (seller == null)
            throw ErrorHelper.Forbidden(ErrorMessages.ProductSellerNotFound);
        if (!seller.IsVerified || seller.Status != SellerStatus.Approved)
            throw ErrorHelper.Forbidden(ErrorMessages.ProductSellerNotVerified);

        if (string.IsNullOrWhiteSpace(seller.CompanyName))
            throw ErrorHelper.BadRequest("Seller chưa cập nhật Company Name, không thể tạo sản phẩm.");


        _logger.Info($"[CreateAsync] Seller {userId} creates new product: {dto.Name}");

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
            Brand = seller.CompanyName ?? "Unknown",
            ImageUrls = new List<string>(),
            SellerId = seller.Id,
            Seller = seller,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            IsDeleted = false,
            Status = dto.Status
        };

        var result = await _unitOfWork.Products.AddAsync(product);
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
                result.ImageUrls = uploadedUrls.Distinct().ToList();
                await _unitOfWork.Products.Update(result);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        await RemoveProductCacheAsync(result.Id, seller.Id);
        _logger.Success(string.Format(ErrorMessages.ProductCreatedLog, result.Name, result.ImageUrls.Count));

        return await GetByIdAsync(product.Id);
    }

    public async Task<ProducDetailstDto> UpdateAsync(Guid id, ProductUpdateDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn(string.Format(ErrorMessages.ProductUpdateNotFoundLog, id));
            throw ErrorHelper.NotFound(ErrorMessages.ProductNotFound);
        }

        _logger.Info(string.Format(ErrorMessages.ProductUpdateLog, userId, product.Name));

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
        if (dto.Height.HasValue)
            product.Height = dto.Height.Value;
        if (dto.Material != null)
            product.Material = dto.Material;
        if (dto.ProductType.HasValue)
            product.ProductType = dto.ProductType.Value;
        if (dto.ProductStatus.HasValue) product.Status = dto.ProductStatus.Value;

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        _logger.Success(string.Format(ErrorMessages.ProductUpdateSuccessLog, id, userId));
        return await GetByIdAsync(product.Id);
    }

    public async Task<ProducDetailstDto> DeleteAsync(Guid id)
    {
        var userId = _claimsService.CurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn(string.Format(ErrorMessages.ProductDeleteNotFoundLog, id));
            throw ErrorHelper.NotFound(ErrorMessages.ProductNotFound);
        }

        product.Status = ProductStatus.InActive;
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.DeletedBy = userId;

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        await RemoveProductCacheAsync(id, product.SellerId);

        _logger.Success(string.Format(ErrorMessages.ProductDeleteSuccessLog, id, userId));
        return await GetByIdAsync(product.Id);
    }

    public async Task<string?> UploadProductImageAsync(Guid productId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.Warn(ErrorMessages.ProductImageFileInvalidLog);
            throw ErrorHelper.BadRequest(ErrorMessages.ProductImageFileInvalid);
        }

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
        {
            _logger.Warn(string.Format(ErrorMessages.ProductImageNotFoundLog, productId));
            throw ErrorHelper.NotFound(ErrorMessages.ProductNotFoundOrDeleted);
        }

        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"products/product_thumbnails_{productId}_{Guid.NewGuid():N}{fileExtension}";

        _logger.Info(string.Format(ErrorMessages.ProductImageUploadingLog, fileName));

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetPreviewUrlAsync(fileName);
        if (string.IsNullOrEmpty(fileUrl))
        {
            _logger.Error(string.Format(ErrorMessages.ProductImageUrlErrorLog, fileName));
            throw ErrorHelper.Internal(ErrorMessages.ProductImageUrlError);
        }

        product.ImageUrls ??= new List<string>();
        product.ImageUrls.Add(fileUrl);

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.SetAsync($"product:{product.Id}", product, TimeSpan.FromHours(1));

        _logger.Success(string.Format(ErrorMessages.ProductImageUpdateSuccessLog, product.Id, fileUrl));

        return fileUrl;
    }

    public async Task<ProducDetailstDto> UpdateProductImagesAsync(Guid productId, List<IFormFile> images)
    {
        var userId = _claimsService.CurrentUserId;
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound(ErrorMessages.ProductNotFound);

        if (product.ImageUrls != null && product.ImageUrls.Count > 0)
            foreach (var url in product.ImageUrls)
            {
                var fileName = ExtractFileNameFromUrl(url);
                if (!string.IsNullOrEmpty(fileName))
                    await _blobService.DeleteFileAsync(fileName);
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

        return await GetByIdAsync(product.Id);
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


    private async Task RemoveProductCacheAsync(Guid productId, Guid sellerId)
    {
        await _cacheService.RemoveAsync($"product:{productId}");
        await _cacheService.RemoveByPatternAsync($"product:all:{sellerId}");
    }

    private ProducDetailstDto MapProductToDetailDto(Product product)
    {
        var dto = _mapper.Map<Product, ProducDetailstDto>(product);
        dto.ProductStockStatus = product.Stock > 0 ? StockStatus.InStock : StockStatus.OutOfStock;
        dto.Brand = product.Seller.CompanyName;
        return dto;
    }

    private string ExtractFileNameFromUrl(string url)
    {
        var uri = new Uri(url);
        var query = HttpUtility.ParseQueryString(uri.Query);
        var prefix = query.Get("prefix");
        return prefix != null ? Uri.UnescapeDataString(prefix) : null;
    }

    #endregion
}