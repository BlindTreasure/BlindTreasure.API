using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserService _userService;

    public CategoryService(
        IUnitOfWork unitOfWork,
        ILoggerService logger,
        ICacheService cacheService,
        IClaimsService claimsService,
        IUserService userService, IBlobService blobService, IMapperService mapper)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _claimsService = claimsService;
        _userService = userService;
        _blobService = blobService;
        _mapper = mapper;
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id)
    {
        var cacheKey = $"category:{id}";
        var cached = await _cacheService.GetAsync<Category>(cacheKey);
        if (cached != null)
        {
            _logger.Info($"[GetByIdAsync] Cache hit for category {id}");
            return ToCategoryDto(cached);
        }

        var category = await _unitOfWork.Categories.GetQueryable()
            .Include(c => c.Parent)
            .Include(c => c.Children.Where(ch => !ch.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            _logger.Warn($"[GetByIdAsync] Category {id} not found.");
            throw ErrorHelper.NotFound(ErrorMessages.CategoryNotFound);
        }

        await _cacheService.SetAsync(cacheKey, category, TimeSpan.FromHours(1));
        _logger.Info($"[GetByIdAsync] Category {id} loaded from DB and cached.");
        return ToCategoryDto(category);
    }

    public async Task<Pagination<CategoryDto>> GetAllAsync(CategoryQueryParameter param)
    {
        _logger.Info(
            $"[GetAllAsync] Admin/Staff requests category list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Categories.GetQueryable()
            .Include(c => c.Children.Where(ch => !ch.IsDeleted))
            .Where(c => !c.IsDeleted && c.ParentId == null)
            .AsNoTracking();

        var keyword = param.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(keyword)) query = query.Where(c => c.Name.ToLower().Contains(keyword));

        query = ApplySort(query, param);

        var count = await query.CountAsync();

        List<Category> items;
        if (param.PageIndex == 0)
            // Trả về toàn bộ danh sách
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(ToCategoryDto).ToList();
        var result = new Pagination<CategoryDto>(dtos, count, param.PageIndex, param.PageSize);

        var cacheKey =
            $"category:all:{param.PageIndex}:{param.PageSize}:{param.Search}:{param.SortBy}{(param.Desc ? "Desc" : "Asc")}";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));

        _logger.Info("[GetAllAsync] Category list loaded from DB and cached.");
        return result;
    }

    public async Task<List<CategoryWithProductsDto>> GetCategoriesWithAllProductsAsync()
    {
        // Lấy tất cả category cấp cha
        var parentCategories = await _unitOfWork.Categories.GetQueryable()
            .Where(c => !c.IsDeleted && c.ParentId == null)
            .Include(c => c.Children.Where(ch => !ch.IsDeleted))
            .ThenInclude(ch => ch.Products.Where(p => !p.IsDeleted))
            .Include(c => c.Products.Where(p => !p.IsDeleted))
            .AsNoTracking()
            .ToListAsync();

        var result = parentCategories.Select(parent =>
        {
            var childProducts = parent.Children
                .SelectMany(ch => ch.Products)
                .Where(p => !p.IsDeleted)
                .ToList();

            var allProducts = parent.Products.Concat(childProducts).ToList();

            return new CategoryWithProductsDto
            {
                Id = parent.Id,
                Name = parent.Name,
                Products = allProducts.Select(p => _mapper.Map<Product, ProducDetailstDto>(p)).ToList(),
                ProductCount = allProducts.Count
            };
        }).ToList();

        return result;
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserDetailsByIdAsync(userId);
        _logger.Info($"[CreateAsync] Admin/Staff creates category {dto.Name} by {user?.FullName}");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw ErrorHelper.BadRequest(ErrorMessages.CategoryNameRequired);

        var exists = await _unitOfWork.Categories.GetQueryable().Where(x => !x.IsDeleted)
            .AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower());
        if (exists)
            throw ErrorHelper.Conflict(ErrorMessages.CategoryNameAlreadyExists);

        if (dto.ParentId.HasValue)
            if (!await _unitOfWork.Categories.GetQueryable().AnyAsync(c => c.Id == dto.ParentId.Value))
                throw ErrorHelper.BadRequest(ErrorMessages.CategoryParentIdInvalid);

        var category = new Category
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            ParentId = dto.ParentId
        };

        if (dto.ImageFile != null)
        {
            if (dto.ParentId != null)
                throw ErrorHelper.BadRequest(ErrorMessages.CategoryImageOnlyForRoot);

            try
            {
                var fileName = $"category-thumbnails/{Guid.NewGuid()}{Path.GetExtension(dto.ImageFile.FileName)}";
                _logger.Info($"[CreateAsync] Uploading image {fileName}");

                await _blobService.UploadFileAsync(fileName, dto.ImageFile.OpenReadStream());
                category.ImageUrl = await _blobService.GetPreviewUrlAsync(fileName);

                _logger.Info($"[CreateAsync] Image uploaded and preview URL: {category.ImageUrl}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[CreateAsync] Upload image failed: {ex.Message}");
                throw ErrorHelper.Internal(ErrorMessages.CategoryImageUploadError);
            }
        }

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        await RemoveCategoryCacheAsync(category.Id);
        _logger.Success($"[CreateAsync] Category {category.Name} created.");
        return ToCategoryDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CategoryUpdateDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserDetailsByIdAsync(userId);
        if (user == null || (user.RoleName != RoleType.Admin && user.RoleName != RoleType.Staff))
            throw ErrorHelper.Forbidden(ErrorMessages.CategoryNoUpdatePermission);

        _logger.Info($"[UpdateAsync] Admin/Staff updates category {dto.Name ?? "(no name change)"} by {user.FullName}");

        var category = await _unitOfWork.Categories.GetQueryable()
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
            throw ErrorHelper.NotFound(ErrorMessages.CategoryNotFound);

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            var exists = await _unitOfWork.Categories.GetQueryable().Where(x => !x.IsDeleted)
                .AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower() && c.Id != id);
            if (exists)
                throw ErrorHelper.Conflict(ErrorMessages.CategoryNameAlreadyExists);

            category.Name = dto.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.Description))
            category.Description = dto.Description.Trim();

        var isBecomingChild = category.ParentId == null && dto.ParentId.HasValue;

        if (dto.ParentId.HasValue)
        {
            if (dto.ParentId.Value == id)
                throw ErrorHelper.BadRequest(ErrorMessages.CategoryParentIdSelf);

            if (await IsDescendantAsync(id, dto.ParentId.Value))
                throw ErrorHelper.BadRequest(ErrorMessages.CategoryHierarchyLoop);

            category.ParentId = dto.ParentId;

            if (isBecomingChild && !string.IsNullOrWhiteSpace(category.ImageUrl))
            {
                try
                {
                    var oldFileName = Path.GetFileName(new Uri(category.ImageUrl).LocalPath);
                    _logger.Info($"[UpdateAsync] Deleting image due to parent assignment: {oldFileName}");

                    await _blobService.DeleteFileAsync($"category-thumbnails/{oldFileName}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[UpdateAsync] Failed to delete image during demotion to child: {ex.Message}");
                }

                category.ImageUrl = null;
            }
        }
        else
        {
            category.ParentId = null;
        }

        if (dto.ImageFile != null)
        {
            if (category.ParentId != null)
                throw ErrorHelper.BadRequest(ErrorMessages.CategoryImageOnlyForRoot);

            try
            {
                if (!string.IsNullOrWhiteSpace(category.ImageUrl))
                {
                    var oldFileName = Path.GetFileName(new Uri(category.ImageUrl).LocalPath);
                    _logger.Info($"[UpdateAsync] Deleting old image: {oldFileName}");

                    await _blobService.DeleteFileAsync($"category-thumbnails/{oldFileName}");
                }

                var newFileName = $"category-thumbnails/{Guid.NewGuid()}{Path.GetExtension(dto.ImageFile.FileName)}";
                _logger.Info($"[UpdateAsync] Uploading new image {newFileName}");

                await _blobService.UploadFileAsync(newFileName, dto.ImageFile.OpenReadStream());
                category.ImageUrl = await _blobService.GetPreviewUrlAsync(newFileName);

                _logger.Info($"[UpdateAsync] Image uploaded and preview URL: {category.ImageUrl}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[UpdateAsync] Upload image failed: {ex.Message}");
                throw ErrorHelper.Internal(ErrorMessages.CategoryImageUploadError);
            }
        }

        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = userId;

        await _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync();

        await RemoveCategoryCacheAsync(id);
        _logger.Success($"[UpdateAsync] Category {id} updated.");

        return ToCategoryDto(category);
    }

    public async Task<CategoryDto> DeleteAsync(Guid id)
    {
        var userId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserDetailsByIdAsync(userId);
        if (user == null || (user.RoleName != RoleType.Admin && user.RoleName != RoleType.Staff))
            throw ErrorHelper.Forbidden(ErrorMessages.CategoryNoDeletePermission);

        var category = await _unitOfWork.Categories.GetQueryable()
            .Include(c => c.Products)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            _logger.Warn($"[DeleteAsync] Category {id} not found.");
            throw ErrorHelper.NotFound(ErrorMessages.CategoryNotFound);
        }

        _logger.Info($"[DeleteAsync] Admin/Staff deletes category {id} by {user.FullName}");

        if ((category.Products != null && category.Products.Any()) ||
            (category.Children != null && category.Children.Any(c => !c.IsDeleted)))
            throw ErrorHelper.Conflict(ErrorMessages.CategoryDeleteHasChildrenOrProducts);

        await _unitOfWork.Categories.SoftRemove(category);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync($"category:{id}");
        await _cacheService.RemoveByPatternAsync("category:all");
        _logger.Success($"[DeleteAsync] Category {id} deleted.");

        return ToCategoryDto(category);
    }

    public async Task<Category?> GetWithParentAsync(Guid categoryId)
    {
        return await _unitOfWork.Categories.GetQueryable()
            .Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.Id == categoryId && !c.IsDeleted);
    }

    public async Task<List<Guid>> GetAllChildCategoryIdsAsync(Guid parentId)
    {
        var allCategories = await _unitOfWork.Categories.GetQueryable()
            .Where(c => !c.IsDeleted)
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync();

        var childCategoryIds = new List<Guid>();

        void Traverse(Guid id)
        {
            var children = allCategories.Where(c => c.ParentId == id).ToList();
            foreach (var child in children)
            {
                childCategoryIds.Add(child.Id);
                Traverse(child.Id);
            }
        }

        childCategoryIds.Add(parentId); // include category gốc
        Traverse(parentId);

        return childCategoryIds;
    }

    #region private methods

    private static CategoryDto ToCategoryDto(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId,
            CreatedAt = category.CreatedAt,
            IsDeleted = category.IsDeleted,
            ImageUrl = category.ImageUrl,
            Children = category.Children != null
                ? category.Children
                    .Where(c => !c.IsDeleted)
                    .Select(ToCategoryDto)
                    .ToList()
                : new List<CategoryDto>()
        };
    }

    private async Task<bool> IsDescendantAsync(Guid categoryId, Guid parentId)
    {
        var current = await _unitOfWork.Categories.GetByIdAsync(parentId);
        while (current != null)
        {
            if (current.ParentId == null) return false;
            if (current.ParentId == categoryId) return true;
            current = await _unitOfWork.Categories.GetByIdAsync(current.ParentId.Value);
        }

        return false;
    }

    private async Task RemoveCategoryCacheAsync(Guid categoryId)
    {
        await _cacheService.RemoveAsync($"category:{categoryId}");
        await _cacheService.RemoveByPatternAsync("category:all");
    }

    private IQueryable<Category> ApplySort(IQueryable<Category> query, CategoryQueryParameter param)
    {
        query = query.OrderByDescending(c => c.ParentId == null);

        return param.SortBy switch
        {
            CategorySortField.Name => param.Desc
                ? ((IOrderedQueryable<Category>)query).ThenByDescending(c => c.Name)
                : ((IOrderedQueryable<Category>)query).ThenBy(c => c.Name),

            _ => param.Desc
                ? ((IOrderedQueryable<Category>)query).ThenByDescending(c => c.CreatedAt)
                : ((IOrderedQueryable<Category>)query).ThenBy(c => c.CreatedAt)
        };
    }

    #endregion
}