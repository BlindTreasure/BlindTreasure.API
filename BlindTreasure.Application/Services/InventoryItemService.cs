using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class InventoryItemService : IInventoryItemService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryService _categoryService;


    public InventoryItemService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IProductService productService,
        IUnitOfWork unitOfWork,
        IOrderService orderService,
        ICategoryService categoryService) // added categoryService
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _orderService = orderService;
        _categoryService = categoryService; // initialize categoryService
    }

    public async Task<InventoryItemDto>
        CreateAsync(CreateInventoryItemDto dto, Guid? userId) // specify userId if needed, otherwise use current user
    {
        if (userId.HasValue)
        {
            userId = userId.Value;
        }
        else
        {
            userId = _claimsService.CurrentUserId;
            if (userId == Guid.Empty)
                throw ErrorHelper.Unauthorized(
                    "User ID is required for creating inventory item. Cannot found current user");
        }

        _loggerService.Info($"[CreateAsync] Creating inventory item for user {userId}, product {dto.ProductId}.");
        var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Product not found.");

        var item = new InventoryItem
        {
            UserId = userId.Value,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            Location = dto.Location ?? string.Empty,
            Status = dto.Status ?? "Active"
        };

        var result = await _unitOfWork.InventoryItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate cache for this item (should not exist, but for safety)
        await _cacheService.RemoveAsync(GetCacheKey(item.Id));

        _loggerService.Success($"[CreateAsync] Inventory item created for user {userId}, product {product.Name}.");
        return InventoryItemMapper.ToInventoryItemDto(result) ??
               throw ErrorHelper.Internal("Failed to create inventory item.");
    }

    public async Task<InventoryItemDto?> GetByIdAsync(Guid id)
    {
        var cacheKey = GetCacheKey(id);
        var cached = await _cacheService.GetAsync<InventoryItem>(cacheKey);
        if (cached != null && !cached.IsDeleted)
        {
            _loggerService.Info($"[GetByIdAsync] Cache hit for inventory item {id}");
            return InventoryItemMapper.ToInventoryItemDto(cached);
        }

        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id, i => i.Product);
        if (item == null || item.IsDeleted)
            return null;

        await _cacheService.SetAsync(cacheKey, item, TimeSpan.FromMinutes(30));
        _loggerService.Info($"[GetByIdAsync] Inventory item {id} loaded from DB and cached.");
        return InventoryItemMapper.ToInventoryItemDto(item);
    }

    public async Task<Pagination<InventoryItemDto>> GetMyInventoryAsync(InventoryItemQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;

        var query = _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => i.UserId == userId && !i.IsDeleted)
            .Include(i => i.Product)
            .ThenInclude(p => p.Category).AsNoTracking();

        // Filter theo tên sản phẩm
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(i => i.Product.Name.ToLower().Contains(keyword));
        }

        // Filter theo category
        if (param.CategoryId.HasValue)
        {
            // Lấy tất cả category con nếu cần
            var categoryIds = await _categoryService.GetAllChildCategoryIdsAsync(param.CategoryId.Value);
            query = query.Where(i => categoryIds.Contains(i.Product.CategoryId));
        }

        // Filter theo status
        if (!string.IsNullOrWhiteSpace(param.Status))
            query = query.Where(i => i.Status == param.Status);

        // Sort: UpdatedAt/CreatedAt theo hướng param.Desc
        if (param.Desc)
            query = query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt);
        else
            query = query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);

        var count = await query.CountAsync();

        List<InventoryItem> items;
        if (param.PageIndex == 0)
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(InventoryItemMapper.ToInventoryItemDto).ToList();
        return new Pagination<InventoryItemDto>(dtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<InventoryItemDto> UpdateAsync(Guid id, UpdateInventoryItemDto dto)
    {
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id, i => i.Product);
        if (item == null || item.IsDeleted)
            throw ErrorHelper.NotFound("Inventory item not found.");

        if (dto.Quantity.HasValue)
            item.Quantity = dto.Quantity.Value;
        if (!string.IsNullOrWhiteSpace(dto.Location))
            item.Location = dto.Location;
        if (!string.IsNullOrWhiteSpace(dto.Status))
            item.Status = dto.Status;

        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = _claimsService.CurrentUserId;

        await _unitOfWork.InventoryItems.Update(item);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate cache
        await _cacheService.RemoveAsync(GetCacheKey(id));

        _loggerService.Success($"[UpdateAsync] Inventory item {id} updated.");
        return await GetByIdAsync(id) ?? throw ErrorHelper.Internal("Failed to update inventory item.");
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id);
        if (item == null || item.IsDeleted)
            throw ErrorHelper.NotFound("Inventory item not found.");

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = _claimsService.CurrentUserId;

        await _unitOfWork.InventoryItems.Update(item);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate cache
        await _cacheService.RemoveAsync(GetCacheKey(id));

        _loggerService.Success($"[DeleteAsync] Inventory item {id} deleted.");
        return true;
    }

    private static string GetCacheKey(Guid id)
    {
        return $"inventoryitem:{id}";
    }
}