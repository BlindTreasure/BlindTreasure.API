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
    public async Task<ProducDetailDto?> GetByIdAsync(Guid id)
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

    public async Task<Pagination<ProducDetailDto>> GetAllAsync(ProductQueryParameter param)
    {
        _logger.Info($"[GetAllAsync] Public requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");

        // Tạo cache key dựa trên các tham số query
        var cacheKey = $"products:list:{param.GetHashCode()}";

        // Thử lấy từ cache trước
        var cachedResult = await _cacheService.GetAsync<Pagination<ProducDetailDto>>(cacheKey);
        if (cachedResult != null)
        {
            _logger.Info($"[GetAllAsync] Cache hit for products list with parameters: {param.GetHashCode()}");
            return cachedResult;
        }

        var baseQuery = _unitOfWork.Products.GetQueryable()
            .Include(p => p.Seller)
            .Where(p => !p.IsDeleted && p.ProductType == ProductSaleType.DirectSale)
            .AsNoTracking();

        var query = await ApplyProductFiltersAndSorts(baseQuery, param);


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
        var result = new Pagination<ProducDetailDto>(dtos, count, param.PageIndex, param.PageSize);

        // Lưu kết quả vào cache với thời gian hết hạn là 5 phút
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
        _logger.Info($"[GetAllAsync] Products list cached with key: {cacheKey}");

        return result;
    }

    public async Task<ProducDetailDto?> CreateAsync(ProductCreateDto dto)
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

        // Xác định status dựa trên stock
        var status = dto.Status;
        if (dto.TotalStockQuantity == 0 && dto.Status != ProductStatus.InActive)
            status = ProductStatus.OutOfStock;

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            CategoryId = dto.CategoryId,
            RealSellingPrice = dto.RealSellingPrice,
            TotalStockQuantity = dto.TotalStockQuantity, // NEW
            ReservedInBlindBox = 0, // NEW - mặc định = 0
            Height = dto.Height,
            Material = dto.Material,
            ProductType = dto.ProductType ?? ProductSaleType.DirectSale,
            Brand = seller.CompanyName ?? "Unknown",
            ImageUrls = new List<string>(),
            SellerId = seller.Id,
            Seller = seller,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            IsDeleted = false,
            Status = status
        };

        if (dto.ListedPrice.HasValue)
        {
            if (dto.ListedPrice.Value < product.RealSellingPrice)
            {
                _logger.Warn(string.Format("Listed Price can not be lower than Real Selling Price. Listed Price: {0}, Real Selling Price: {1}",
                    dto.ListedPrice.Value, product.RealSellingPrice));
                throw ErrorHelper.BadRequest("Listed Price can not be lower than Real Selling Price.");
            }
            product.ListedPrice = dto.ListedPrice.Value;
        }
        else
        {
                       // Nếu không có ListedPrice, mặc định sẽ bằng RealSellingPrice
            product.ListedPrice = product.RealSellingPrice;
        }

        // Cập nhật logic xác định status
        if (product.AvailableToSell == 0 && dto.Status != ProductStatus.InActive)
            status = ProductStatus.OutOfStock;

        product.Status = status;

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

    public async Task<ProducDetailDto?> UpdateAsync(Guid id, ProductUpdateDto dto)
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
        if (dto.RealSellingPrice.HasValue)
            product.RealSellingPrice = dto.RealSellingPrice.Value;
        if (dto.ListedPrice.HasValue)
        {
            if (dto.ListedPrice.Value < product.RealSellingPrice)
            {
                _logger.Warn(string.Format("Listed Price can not be lower than Real Selling Price. Listed Price: {0}, Real Selling Price: {1}",
                    dto.ListedPrice.Value, product.RealSellingPrice));
                throw ErrorHelper.BadRequest("Listed Price can not be lower than Real Selling Price.");
            }
            product.ListedPrice = dto.ListedPrice.Value;
        }
            if (dto.TotalStockQuantity.HasValue)
        {
            product.TotalStockQuantity = dto.TotalStockQuantity.Value;
            // Tự động cập nhật status khi stock = 0
            if (dto.TotalStockQuantity.Value == 0 && product.Status != ProductStatus.InActive)
                product.Status = ProductStatus.OutOfStock;
            // Tự động cập nhật status khi stock > 0 và status hiện tại là OutOfStock
            else if (dto.TotalStockQuantity.Value > 0 && product.Status == ProductStatus.OutOfStock)
                product.Status = ProductStatus.Active;
        }

        if (dto.TotalStockQuantity.HasValue)
        {
            product.TotalStockQuantity = dto.TotalStockQuantity.Value;

            // Cập nhật status dựa trên AvailableToSell
            if (product.AvailableToSell == 0 && product.Status != ProductStatus.InActive)
                product.Status = ProductStatus.OutOfStock;
            else if (product.AvailableToSell > 0 && product.Status == ProductStatus.OutOfStock)
                product.Status = ProductStatus.Active;
        }

        if (dto.Height.HasValue)
            product.Height = dto.Height.Value;
        if (dto.Material != null)
            product.Material = dto.Material;
        if (dto.ProductType.HasValue)
            product.ProductType = dto.ProductType.Value;
        if (dto.ProductStatus.HasValue) product.Status = dto.ProductStatus.Value;

        // Cập nhật thông tin UpdatedAt và UpdatedBy
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = userId;

        await _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache liên quan
        await RemoveProductCacheAsync(product.Id, product.SellerId);

        _logger.Success(string.Format(ErrorMessages.ProductUpdateSuccessLog, id, userId));
        return await GetByIdAsync(product.Id);
    }

    public async Task<ProducDetailDto> DeleteAsync(Guid id)
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
        return MapProductToDetailDto(product);
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

    public async Task<ProducDetailDto> UpdateProductImagesAsync(Guid productId, List<IFormFile> images)
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

    private async Task<IQueryable<Product>> ApplyProductFiltersAndSorts(IQueryable<Product> query,
        ProductQueryParameter param)
    {
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
            query = query.Where(p => p.RealSellingPrice >= param.MinPrice.Value);

        if (param.MaxPrice.HasValue)
            query = query.Where(p => p.RealSellingPrice <= param.MaxPrice.Value);

        if (param.ReleaseDateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= param.ReleaseDateFrom.Value);

        if (param.ReleaseDateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= param.ReleaseDateTo.Value);

        // Sort + push OutOfStock (AvailableToSell = 0) to bottom
        if (param.SortBy == null)
            query = query
                .OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0) // AvailableToSell == 0
                .ThenBy(p => p.RealSellingPrice);
        else
            query = param.SortBy switch
            {
                ProductSortField.Name => param.Desc
                    ? query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenByDescending(p => p.Name)
                    : query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenBy(p => p.Name),

                ProductSortField.Price => param.Desc
                    ? query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenByDescending(p => p.RealSellingPrice)
                    : query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenBy(p => p.RealSellingPrice),

                ProductSortField.Stock => param.Desc
                    ? query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenByDescending(p => p.TotalStockQuantity - p.ReservedInBlindBox) // Sort by AvailableToSell
                    : query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenBy(p => p.TotalStockQuantity - p.ReservedInBlindBox), // Sort by AvailableToSell

                ProductSortField.CreatedAt => param.Desc
                    ? query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenByDescending(p => p.CreatedAt)
                    : query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenBy(p => p.CreatedAt),

                _ => param.Desc
                    ? query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                    : query.OrderBy(p => p.TotalStockQuantity - p.ReservedInBlindBox == 0)
                        .ThenBy(p => p.UpdatedAt ?? p.CreatedAt)
            };

        return query;
    }

    private async Task ValidateProductDto(ProductCreateDto dto)
    {
        _logger.Info(
            $"[ValidateProductDto] Start validating product: Name='{dto.Name}', Description='{dto.Description}', Price={dto.RealSellingPrice}, Stock={dto.TotalStockQuantity}, CategoryId={dto.CategoryId}");

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            _logger.Warn("[ValidateProductDto] Validation failed: 'Name' is null or empty.");
            throw ErrorHelper.BadRequest("Tên sản phẩm không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            _logger.Warn("[ValidateProductDto] Validation failed: 'Description' is null or empty.");
            throw ErrorHelper.BadRequest("Mô tả không được để trống.");
        }

        if (dto.RealSellingPrice <= 0)
        {
            _logger.Warn($"[ValidateProductDto] Validation failed: 'Price' must be > 0. Input value: {dto.RealSellingPrice}");
            throw ErrorHelper.BadRequest("Giá sản phẩm phải lớn hơn 0.");
        }

        if (dto.TotalStockQuantity < 0)
        {
            _logger.Warn(
                $"[ValidateProductDto] Validation failed: 'Stock' must be >= 0. Input value: {dto.TotalStockQuantity}");
            throw ErrorHelper.BadRequest("Số lượng tồn kho phải >= 0.");
        }

        var categoryExists = await _unitOfWork.Categories.GetQueryable()
            .AnyAsync(c => c.Id == dto.CategoryId && !c.IsDeleted);

        if (!categoryExists)
        {
            _logger.Warn(
                $"[ValidateProductDto] Validation failed: Category '{dto.CategoryId}' does not exist or is deleted.");
            throw ErrorHelper.BadRequest("Danh mục sản phẩm không hợp lệ.");
        }

        _logger.Success($"[ValidateProductDto] Validation passed for product: Name='{dto.Name}'");
    }


    private async Task RemoveProductCacheAsync(Guid productId, Guid sellerId)
    {
        // Xóa cache chi tiết sản phẩm
        await _cacheService.RemoveAsync($"product:{productId}");

        // Xóa cache danh sách sản phẩm của seller
        await _cacheService.RemoveByPatternAsync($"product:all:{sellerId}");

        // Xóa cache danh sách sản phẩm (tất cả các pattern có thể)
        await _cacheService.RemoveByPatternAsync("products:list:*");
    }

    private ProducDetailDto MapProductToDetailDto(Product product)
    {
        var dto = _mapper.Map<Product, ProducDetailDto>(product);
        dto.ProductStockStatus = product.AvailableToSell > 0 ? StockStatus.InStock : StockStatus.OutOfStock;
        dto.AvailableToSell = product.AvailableToSell; // Map computed field
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