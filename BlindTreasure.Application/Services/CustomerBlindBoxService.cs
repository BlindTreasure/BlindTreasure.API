using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

/// <summary>
///     Service quản lý kho BlindBox đã mua của user (CustomerInventory).
///     Lưu trữ các BlindBox đã thanh toán, hỗ trợ lấy danh sách, chi tiết, cập nhật trạng thái mở box, xóa mềm.
/// </summary>
public class CustomerBlindBoxService : ICustomerBlindBoxService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerBlindBoxService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IProductService productService,
        IUnitOfWork unitOfWork,
        IOrderService orderService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _orderService = orderService;
    }

    /// <summary>
    ///     Thêm 1 BlindBox vào kho của user (sau khi thanh toán thành công).
    /// </summary>
    public async Task<CustomerInventoryDto> CreateAsync(CreateCustomerInventoryDto dto, Guid? userId = null)
    {
        var uid = userId ?? _claimsService.CurrentUserId;
        if (uid == Guid.Empty)
            throw ErrorHelper.Unauthorized("User ID is required for creating customer inventory.");

        var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(dto.BlindBoxId);
        if (blindBox == null || blindBox.IsDeleted)
            throw ErrorHelper.NotFound("BlindBox not found.");

        var entity = new CustomerBlindBox
        {
            UserId = uid,
            BlindBoxId = dto.BlindBoxId,
            OrderDetailId = dto.OrderDetailId,
            IsOpened = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = uid
        };

        var result = await _unitOfWork.CustomerBlindBoxes.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync(GetCacheKey(entity.Id));
        _loggerService.Success($"[CreateAsync] CustomerInventory created for user {uid}, blindbox {blindBox.Id}.");
        return CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto(result);
    }

    /// <summary>
    ///     Lấy chi tiết 1 BlindBox trong kho user theo Id.
    /// </summary>
    public async Task<CustomerInventoryDto?> GetByIdAsync(Guid id)
    {
        var cacheKey = GetCacheKey(id);
        var cached = await _cacheService.GetAsync<CustomerBlindBox>(cacheKey);
        if (cached != null && !cached.IsDeleted)
        {
            _loggerService.Info($"[GetByIdAsync] Cache hit for customer inventory {id}");
            return CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto(cached);
        }

        var entity = await _unitOfWork.CustomerBlindBoxes.GetByIdAsync(id, x => x.BlindBox, x => x.OrderDetail);
        if (entity == null || entity.IsDeleted)
            return null;

        await _cacheService.SetAsync(cacheKey, entity, TimeSpan.FromMinutes(30));
        _loggerService.Info($"[GetByIdAsync] Customer inventory {id} loaded from DB and cached.");
        return CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto(entity);
    }

    /// <summary>
    ///     Lấy toàn bộ BlindBox đã mua của user hiện tại.
    /// </summary>
    public async Task<Pagination<CustomerInventoryDto>> GetMyBlindBoxesAsync(CustomerBlindBoxQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;

        var query = _unitOfWork.CustomerBlindBoxes.GetQueryable()
            .Where(i => i.UserId == userId && !i.IsDeleted)
            .Include(i => i.BlindBox)
            .Include(i => i.OrderDetail).AsNoTracking();

        // Filter theo IsOpened
        if (param.IsOpened.HasValue)
            query = query.Where(i => i.IsOpened == param.IsOpened.Value);

        // Filter theo tên BlindBox
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(i => i.BlindBox.Name.ToLower().Contains(keyword));
        }

        // Filter theo BlindBoxId
        if (param.BlindBoxId.HasValue)
            query = query.Where(i => i.BlindBoxId == param.BlindBoxId.Value);

        // Sort: UpdatedAt/CreatedAt theo hướng param.Desc
        if (param.Desc)
            query = query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt);
        else
            query = query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);

        var count = await query.CountAsync();

        List<CustomerBlindBox> items;
        if (param.PageIndex == 0)
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto).ToList();
        return new Pagination<CustomerInventoryDto>(dtos, count, param.PageIndex, param.PageSize);
    }

    /// <summary>
    ///     Đánh dấu BlindBox đã mở (IsOpened = true).
    /// </summary>
    public async Task<CustomerInventoryDto> MarkAsOpenedAsync(Guid id)
    {
        var entity = await _unitOfWork.CustomerBlindBoxes.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
            throw ErrorHelper.NotFound("Customer inventory not found.");

        entity.IsOpened = true;
        entity.OpenedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _claimsService.CurrentUserId;

        await _unitOfWork.CustomerBlindBoxes.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync(GetCacheKey(id));
        _loggerService.Success($"[MarkAsOpenedAsync] Customer inventory {id} marked as opened.");
        return await GetByIdAsync(id) ?? throw ErrorHelper.Internal("Failed to update customer inventory.");
    }

    /// <summary>
    ///     Xóa mềm 1 BlindBox khỏi kho user.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _unitOfWork.CustomerBlindBoxes.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
            throw ErrorHelper.NotFound("Customer inventory not found.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _claimsService.CurrentUserId;

        await _unitOfWork.CustomerBlindBoxes.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await _cacheService.RemoveAsync(GetCacheKey(id));
        _loggerService.Success($"[DeleteAsync] Customer inventory {id} deleted.");
        return true;
    }

    private static string GetCacheKey(Guid id)
    {
        return $"customerinventory:{id}";
    }
}