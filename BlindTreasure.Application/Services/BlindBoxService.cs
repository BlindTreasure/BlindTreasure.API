using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class BlindBoxService : IBlindBoxService
{
    private readonly IClaimsService _claimsService;
    private readonly IMapperService _mapperService;
    private readonly ICurrentTime _time;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;

    public BlindBoxService(IUnitOfWork unitOfWork, IClaimsService claimsService, ICurrentTime time,
        IMapperService mapperService, IBlobService blobService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _time = time;
        _mapperService = mapperService;
        _blobService = blobService;
    }

    public async Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.GetQueryable()
            .Where(x => x.Id == blindBoxId && !x.IsDeleted)
            .Include(b => b.BlindBoxItems)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync();

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

    public async Task<BlindBoxDetailDto> CreateBlindBoxAsync(CreateBlindBoxDto dto)
    {
        if (dto == null)
            throw ErrorHelper.BadRequest("Dữ liệu Blind Box không được để trống.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw ErrorHelper.BadRequest("Tên Blind Box là bắt buộc.");

        if (dto.Price <= 0)
            throw ErrorHelper.BadRequest("Giá Blind Box phải lớn hơn 0.");

        if (dto.TotalQuantity <= 0)
            throw ErrorHelper.BadRequest("Tổng số lượng phải lớn hơn 0.");

        if (dto.ReleaseDate == default)
            throw ErrorHelper.BadRequest("Ngày phát hành không hợp lệ.");

        if (dto.ImageFile == null || dto.ImageFile.Length == 0)
            throw ErrorHelper.BadRequest("Ảnh đại diện Blind Box là bắt buộc.");

        var currentUserId = _claimsService.GetCurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.UserId == currentUserId && !s.IsDeleted && s.Status == SellerStatus.Approved);

        if (seller == null)
            throw ErrorHelper.Forbidden("Bạn chưa được xác minh Seller để tạo Blind Box.");

        // Upload file ảnh lên BlobStorage
        var fileName = $"blindbox-thumbnails/thumbnails-{Guid.NewGuid()}{Path.GetExtension(dto.ImageFile.FileName)}";
        await using var stream = dto.ImageFile.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        // Lấy link file đã upload
        string imageUrl = await _blobService.GetFileUrlAsync(fileName);
        if (string.IsNullOrEmpty(imageUrl))
            throw ErrorHelper.Internal("Lỗi khi lấy URL ảnh Blind Box.");

        var releaseDateUtc = DateTime.SpecifyKind(dto.ReleaseDate, DateTimeKind.Utc);

        
        var blindBox = new BlindBox
        {
            Id = Guid.NewGuid(),
            SellerId = seller.Id,
            Name = dto.Name.Trim(),
            Price = dto.Price,
            TotalQuantity = dto.TotalQuantity,
            Description = dto.Description.Trim(),
            ImageUrl = imageUrl,
            ReleaseDate = releaseDateUtc,
            HasSecretItem = dto.HasSecretItem,
            SecretProbability = dto.SecretProbability,
            Status = BlindBoxStatus.Draft,
            CreatedAt = _time.GetCurrentTime(),
            CreatedBy = currentUserId
        };


        await _unitOfWork.BlindBoxes.AddAsync(blindBox);
        await _unitOfWork.SaveChangesAsync();

        return await GetBlindBoxByIdAsync(blindBox.Id);
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