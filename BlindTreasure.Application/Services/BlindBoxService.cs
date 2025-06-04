using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class BlindBoxService : IBlindBoxService
{
    private readonly IClaimsService _claimsService;
    private readonly IMapperService _mapperService;
    private readonly ICurrentTime _time;
    private readonly IUnitOfWork _unitOfWork;

    public BlindBoxService(IUnitOfWork unitOfWork, IClaimsService claimsService, ICurrentTime time,
        IMapperService mapperService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _time = time;
        _mapperService = mapperService;
    }

    public async Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems,
            b => b.BlindBoxItems.Select(i => i.Product)
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound("Blind Box không tồn tại");

        var result = _mapperService.Map<BlindBox, BlindBoxDetailDto>(blindBox);

        result.Items = blindBox.BlindBoxItems.Select(item => new BlindBoxItemDto
        {
            ProductId = item.ProductId,
            ProductName = item.Product?.Name ?? "",
            DropRate = item.DropRate,
            Quantity = item.Quantity,
            Rarity = item.Rarity
        }).ToList();

        return result;
    }

    public async Task<BlindBoxDetailDto> AddItemsToBlindBoxAsync(Guid blindBoxId, List<BlindBoxItemDto> items)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound("Blind Box không tồn tại");

        var currentUserId = _claimsService.GetCurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(x =>
            x.Id == blindBox.SellerId && x.UserId == currentUserId && !x.IsDeleted);

        if (seller == null)
            throw ErrorHelper.Forbidden("Không có quyền chỉnh sửa Blind Box này");

        var productIds = items.Select(i => i.ProductId).ToList();

        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) &&
            p.SellerId == seller.Id &&
            p.Stock > 0 &&
            !p.IsDeleted);

        if (products.Count != items.Count)
            throw ErrorHelper.BadRequest("Một hoặc nhiều sản phẩm không hợp lệ hoặc đã hết hàng");

        var dropRateTotal = items.Sum(i => i.DropRate);
        if (dropRateTotal <= 0)
            throw ErrorHelper.BadRequest("Tổng DropRate phải lớn hơn 0");

        var now = _time.GetCurrentTime();

        var entities = items.Select(i => new BlindBoxItem
        {
            Id = Guid.NewGuid(),
            BlindBoxId = blindBoxId,
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            DropRate = i.DropRate,
            Rarity = i.Rarity,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = currentUserId
        }).ToList();

        await _unitOfWork.BlindBoxItems.AddRangeAsync(entities);
        await _unitOfWork.SaveChangesAsync();

        return await GetBlindBoxByIdAsync(blindBoxId);
    }

    public async Task<bool> SubmitBlindBoxAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound("Blind Box không tồn tại");

        if (blindBox.BlindBoxItems == null || !blindBox.BlindBoxItems.Any())
            throw ErrorHelper.BadRequest("Phải có ít nhất 1 item trong Blind Box");

        var totalDropRate = blindBox.BlindBoxItems.Sum(i => i.DropRate);
        if (totalDropRate < 100)
            throw ErrorHelper.BadRequest("Tổng DropRate chưa đủ 100%");

        blindBox.UpdatedAt = _time.GetCurrentTime();
        blindBox.Status = BlindBoxStatus.PendingApproval;

        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}