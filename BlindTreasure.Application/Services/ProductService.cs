using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ProductService : IProductService
{
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
        IMapperService mapper)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _claimsService = claimsService;
        _mapper = mapper;
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

    public async Task<Pagination<ProductDto>> GetAllAsync(PaginationParameter param)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");

        _logger.Info(
            $"[GetAllAsync] Seller {userId} requests product list. Page: {param.PageIndex}, Size: {param.PageSize}");

        if (param.PageIndex <= 0 || param.PageSize <= 0)
            throw ErrorHelper.BadRequest("Thông số phân trang không hợp lệ. PageIndex và PageSize phải lớn hơn 0.");

        var cacheKey = $"product:all:{seller.Id}:{param.PageIndex}:{param.PageSize}";
        var cached = await _cacheService.GetAsync<Pagination<ProductDto>>(cacheKey);
        if (cached != null)
        {
            _logger.Info("[GetAllAsync] Cache hit for product list.");
            return cached;
        }

        var query = _unitOfWork.Products.GetQueryable()
            .Where(p => !p.IsDeleted && p.SellerId == seller.Id)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking();

        var count = await query.CountAsync();
        if (count == 0)
            throw ErrorHelper.NotFound("Không tìm thấy sản phẩm nào.");

        var items = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var dtos = items.Select(p => _mapper.Map<Product, ProductDto>(p)).ToList();
        var result = new Pagination<ProductDto>(dtos, count, param.PageIndex, param.PageSize);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _logger.Info("[GetAllAsync] Product list loaded from DB and cached.");
        return result;
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto)
    {
        var userId = _claimsService.GetCurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null || !seller.IsVerified)
            throw ErrorHelper.Forbidden("Seller chưa được xác minh.");
        _logger.Info($"[CreateAsync] Seller {userId} creates product {dto.Name}");

        await ValidateProductDto(dto);

        var product = _mapper.Map<ProductCreateDto, Product>(dto);
        product.SellerId = seller.Id;
        product.CreatedAt = DateTime.UtcNow;
        product.CreatedBy = userId;

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

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

    // ----------------- PRIVATE HELPER METHODS -----------------

    private async Task<Guid> GetSellerIdByUserId(Guid userId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null)
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
}