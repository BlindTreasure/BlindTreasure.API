using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserService _userService;

    public CategoryService(
        IUnitOfWork unitOfWork,
        ILoggerService logger,
        ICacheService cacheService,
        IClaimsService claimsService,
        IUserService userService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _claimsService = claimsService;
        _userService = userService;
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
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            _logger.Warn($"[GetByIdAsync] Category {id} not found.");
            throw ErrorHelper.NotFound("Không tìm thấy category.");
        }

        await _cacheService.SetAsync(cacheKey, category, TimeSpan.FromHours(1));
        _logger.Info($"[GetByIdAsync] Category {id} loaded from DB and cached.");
        return ToCategoryDto(category);
    }

    public async Task<Pagination<CategoryDto>> GetAllAsync(CategoryQueryParameter param)
    {
        _logger.Info($"[GetAllAsync] Admin/Staff requests category list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Categories.GetQueryable()
            .Where(c => !c.IsDeleted)
            .AsNoTracking();

        // Filter
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(keyword));
        }

        // Sort: UpdatedAt desc, CreatedAt desc
        query = query.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt);

        var count = await query.CountAsync();
        if (count == 0)
           _logger.Info("Không tìm thấy category nào.");

        List<Category> items;
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

        var dtos = items.Select(ToCategoryDto).ToList();
        var result = new Pagination<CategoryDto>(dtos, count, param.PageIndex, param.PageSize);

        var cacheKey = $"category:all:{param.PageIndex}:{param.PageSize}:{param.Search}:UpdatedAtDesc";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        _logger.Info("[GetAllAsync] Category list loaded from DB and cached.");
        return result;
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto)
    {
        var userId = _claimsService.GetCurrentUserId;
        var user = await _userService.GetUserDetailsByIdAsync(userId);
        //if (user == null || (user.RoleName != RoleType.Admin && user.RoleName != RoleType.Staff))
        //    throw ErrorHelper.Forbidden("Bạn không có quyền tạo danh mục.");
        _logger.Info($"[CreateAsync] Admin/Staff creates category {dto.Name} by {user.FullName}");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw ErrorHelper.BadRequest("Tên category không được để trống.");

        // Validate tên duy nhất
        var exists = await _unitOfWork.Categories.GetQueryable().Where(x => x.IsDeleted == false)
            .AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower());
        if (exists)
            throw ErrorHelper.Conflict("Tên danh mục đã tồn tại trong hệ thống.");

        // Validate ParentId nếu có
        if (dto.ParentId.HasValue)
            if (!await _unitOfWork.Categories.GetQueryable().AnyAsync(c => c.Id == dto.ParentId.Value))
                throw ErrorHelper.BadRequest("ParentId không hợp lệ.");

        var category = new Category
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            ParentId = dto.ParentId
        };

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        await RemoveCategoryCacheAsync(category.Id);
        _logger.Success($"[CreateAsync] Category {category.Name} created.");
        return ToCategoryDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CategoryUpdateDto dto)
    {
        var userId = _claimsService.GetCurrentUserId;
        var user = await _userService.GetUserDetailsByIdAsync(userId);
        if (user == null || (user.RoleName != RoleType.Admin && user.RoleName != RoleType.Staff))
            throw ErrorHelper.Forbidden("Bạn không có quyền update danh mục.");
        _logger.Info($"[UpdateAsync] Admin/Staff updates category {dto.Name ?? "(no name change)"} by {user.FullName}");

        var category = await _unitOfWork.Categories.GetQueryable()
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
            throw ErrorHelper.NotFound("Không tìm thấy category.");

        // Chỉ cập nhật trường có giá trị khác null
        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            var exists = await _unitOfWork.Categories.GetQueryable().Where(x => x.IsDeleted == false)
                .AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower() && c.Id != id);
            if (exists)
                throw ErrorHelper.Conflict("Tên danh mục đã tồn tại trong hệ thống.");

            category.Name = dto.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.Description))
            category.Description = dto.Description.Trim();

        if (dto.ParentId.HasValue)
        {
            if (dto.ParentId.Value == id)
                throw ErrorHelper.BadRequest("ParentId không được trùng với chính nó.");

            if (await IsDescendantAsync(id, dto.ParentId.Value))
                throw ErrorHelper.BadRequest("Không được tạo vòng lặp phân cấp category.");

            category.ParentId = dto.ParentId;
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
        var userId = _claimsService.GetCurrentUserId;
        var user = await _userService.GetUserDetailsByIdAsync(userId);
        if (user == null || (user.RoleName != RoleType.Admin && user.RoleName != RoleType.Staff))
            throw ErrorHelper.Forbidden("Bạn không có quyền xóa danh mục.");

        var category = await _unitOfWork.Categories.GetQueryable()
            .Include(c => c.Products)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            _logger.Warn($"[DeleteAsync] Category {id} not found.");
            throw ErrorHelper.NotFound("Không tìm thấy category.");
        }

        _logger.Info($"[DeleteAsync] Admin/Staff deletes category {id} by {user.FullName}");

        // Không xóa nếu còn sản phẩm hoặc category con
        if ((category.Products != null && category.Products.Any()) ||
            (category.Children != null && category.Children.Any()))
            throw ErrorHelper.Conflict("Không thể xóa category khi còn sản phẩm hoặc category con liên quan.");

        await _unitOfWork.Categories.SoftRemove(category);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync($"category:{id}");
        await _cacheService.RemoveByPatternAsync("category:all");
        _logger.Success($"[DeleteAsync] Category {id} deleted.");

        // Trả về DTO với trạng thái isDeleted đã cập nhật
        return ToCategoryDto(category);
    }

    private static CategoryDto ToCategoryDto(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId,
            CreatedAt = category.CreatedAt,
            IsDeleted = category.IsDeleted
        };
    }


    /// <summary>
    ///     Kiểm tra xem parentId có nằm trong cây con của categoryId không (để tránh vòng lặp).
    /// </summary>
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
}