using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
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

    public async Task<Pagination<BlindBoxDetailDto>> GetAllBlindBoxesAsync(BlindBoxQueryParameter param)
    {
        if (param == null)
            throw ErrorHelper.BadRequest("Tham số truy vấn không được để trống.");

        if (param.PageIndex < 1)
            throw ErrorHelper.BadRequest("PageIndex phải lớn hơn hoặc bằng 1.");

        if (param.PageSize <= 0)
            throw ErrorHelper.BadRequest("PageSize phải lớn hơn 0.");

        var query = _unitOfWork.BlindBoxes.GetQueryable()
            .Where(b => !b.IsDeleted);

        // Có thể thêm filter theo param, ví dụ tên, trạng thái nếu mở rộng sau

        var totalCount = await query.CountAsync();

        if (totalCount == 0)
            throw ErrorHelper.NotFound("Không tìm thấy Blind Box nào.");

        var blindBoxes = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .Include(b => b.BlindBoxItems)
            .ThenInclude(item => item.Product) // Đảm bảo Include Product để lấy tên
            .ToListAsync();

        var dtos = blindBoxes.Select(b =>
        {
            var dto = _mapperService.Map<BlindBox, BlindBoxDetailDto>(b);
            dto.Items = b.BlindBoxItems?.Select(item => new BlindBoxItemDto
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                ProductName = item.Product?.Name ?? string.Empty,
                DropRate = item.DropRate,
                Rarity = item.Rarity
            }).ToList() ?? new List<BlindBoxItemDto>();

            return dto;
        }).ToList();

        return new Pagination<BlindBoxDetailDto>(dtos, totalCount, param.PageIndex, param.PageSize);
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

        var currentUserId = _claimsService.CurrentUserId;

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
            throw ErrorHelper.NotFound("Blind Box không tồn tại.");

        var currentUserId = _claimsService.CurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(x =>
            x.Id == blindBox.SellerId && x.UserId == currentUserId && !x.IsDeleted);

        if (seller == null)
            throw ErrorHelper.Forbidden("Không có quyền chỉnh sửa Blind Box này.");

        await ValidateBlindBoxItemsAsync(blindBox, seller, items);

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
        if (totalDropRate != 100)
            throw ErrorHelper.BadRequest("Tổng DropRate phải bằng 100%.");

        var itemCount = blindBox.BlindBoxItems.Count;
        if (itemCount != 6 && itemCount != 12)
            throw ErrorHelper.BadRequest("Blind Box phải có đủ 6 hoặc 12 item để gửi duyệt.");

        blindBox.UpdatedAt = _time.GetCurrentTime();
        blindBox.Status = BlindBoxStatus.PendingApproval;

        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
    
    public async Task<List<BlindBoxDetailDto>> GetPendingApprovalBlindBoxesAsync()
    {
        var boxes = await _unitOfWork.BlindBoxes.GetAllAsync(
            b => b.Status == BlindBoxStatus.PendingApproval && !b.IsDeleted,
            b => b.BlindBoxItems,
            b => b.Seller
        );

        if (boxes == null || boxes.Count == 0)
            throw ErrorHelper.NotFound("Không có Blind Box nào đang chờ duyệt.");

        return boxes.Select(b =>
        {
            var dto = _mapperService.Map<BlindBox, BlindBoxDetailDto>(b);
            dto.Items = b.BlindBoxItems.Select(i => new BlindBoxItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                DropRate = i.DropRate,
                Rarity = i.Rarity,
                ProductName = i.Product?.Name ?? ""
            }).ToList();

            return dto;
        }).ToList();
    }

    public async Task<bool> ApproveBlindBoxAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            b => b.Id == blindBoxId && b.Status == BlindBoxStatus.PendingApproval && !b.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound("Blind Box không tồn tại hoặc không hợp lệ.");

        if (blindBox.BlindBoxItems == null || !blindBox.BlindBoxItems.Any())
            throw ErrorHelper.BadRequest("Blind Box không chứa item nào.");

        var totalDropRate = blindBox.BlindBoxItems.Sum(i => i.DropRate);
        if (totalDropRate != 100)
            throw ErrorHelper.BadRequest("Tổng DropRate phải bằng 100%.");

        var currentUserId = _claimsService.CurrentUserId;
        var now = _time.GetCurrentTime();

        // Cập nhật trạng thái Blind Box
        blindBox.Status = BlindBoxStatus.Approved;
        blindBox.UpdatedAt = now;
        blindBox.UpdatedBy = currentUserId;

        await _unitOfWork.BlindBoxes.Update(blindBox);

        // Tạo bản ghi ProbabilityConfig cho từng item
        var configs = blindBox.BlindBoxItems.Select(item => new ProbabilityConfig
        {
            Id = Guid.NewGuid(),
            BlindBoxItemId = item.Id,
            Probability = item.DropRate,
            EffectiveFrom = now,
            EffectiveTo = blindBox.ReleaseDate, // sử dụng ngày phát hành làm mốc kết thúc hiệu lực
            ApprovedBy = currentUserId,
            ApprovedAt = now,
            CreatedAt = now,
            CreatedBy = currentUserId
        }).ToList();

        await _unitOfWork.ProbabilityConfigs.AddRangeAsync(configs);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    
    public async Task<bool> RejectBlindBoxAsync(Guid blindBoxId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw ErrorHelper.BadRequest("Lý do từ chối không được để trống.");

        var box = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            b => b.Id == blindBoxId && b.Status == BlindBoxStatus.PendingApproval && !b.IsDeleted
        );

        if (box == null)
            throw ErrorHelper.NotFound("Blind Box không hợp lệ hoặc không tồn tại.");

        var currentUserId = _claimsService.CurrentUserId;

        box.Status = BlindBoxStatus.Rejected;
        box.RejectReason = reason.Trim();
        box.UpdatedAt = _time.GetCurrentTime();
        box.UpdatedBy = currentUserId;

        await _unitOfWork.BlindBoxes.Update(box);
        await _unitOfWork.SaveChangesAsync();

        // TODO: Thêm gửi thông báo cho Seller nếu cần

        return true;
    }
    
    /// <summary>
    /// 1. Danh sách item không được để trống.
    /// 2. Mỗi sản phẩm phải thuộc Seller hiện tại và còn hàng (Stock > 0, chưa bị xoá).
    /// 3. Số lượng (quantity) của mỗi item không được vượt quá tồn kho (Stock) của sản phẩm tương ứng.
    /// 4. Blind Box phải có ít nhất 1 item loại Secret.
    /// 5. Nếu có item loại Secret thì DropRate cố định là 5% (frontend không được nhập).
    /// 6. Nếu Blind Box không hỗ trợ Secret nhưng có item Secret thì sẽ báo lỗi.
    /// 7. Tổng DropRate của các item (trừ Secret) phải nhỏ hơn 100%.
    /// </summary>
    private async Task ValidateBlindBoxItemsAsync(BlindBox blindBox, Seller seller, List<BlindBoxItemDto> items)
    {
        if (items == null || items.Count == 0)
            throw ErrorHelper.BadRequest("Danh sách item không được để trống.");

        var productIds = items.Select(i => i.ProductId).ToList();

        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) &&
            p.SellerId == seller.Id &&
            p.Stock > 0 &&
            !p.IsDeleted);

        if (products.Count != items.Count)
            throw ErrorHelper.BadRequest("Một hoặc nhiều sản phẩm không hợp lệ hoặc đã hết hàng.");

        // Validate số lượng
        foreach (var item in items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            if (item.Quantity > product.Stock)
                throw ErrorHelper.BadRequest($"Sản phẩm '{product.Name}' vượt quá số lượng tồn kho.");
        }

        // Validate DropRate và xử lý Secret
        decimal totalDropRate = 0;
        bool hasSecret = false;

        foreach (var item in items)
        {
            if (item.Rarity == BlindBoxRarity.Secret)
            {
                if (!blindBox.HasSecretItem)
                    throw ErrorHelper.BadRequest("Blind Box không hỗ trợ Secret item.");

                item.DropRate = 5m; // ép cứng drop rate cho Secret
                hasSecret = true;
            }
            else
            {
                totalDropRate += item.DropRate;
            }
        }

        if (!hasSecret)
            throw ErrorHelper.BadRequest("Blind Box phải có ít nhất 1 item loại Secret.");

        if (totalDropRate >= 100)
            throw ErrorHelper.BadRequest("Tổng DropRate của item (trừ Secret) phải nhỏ hơn 100%.");
    }
}