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
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapperService;
    private readonly ICurrentTime _time;
    private readonly IUnitOfWork _unitOfWork;

    public BlindBoxService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime time,
        IMapperService mapperService,
        IBlobService blobService,
        ICacheService cacheService,
        ILoggerService logger, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _time = time;
        _mapperService = mapperService;
        _blobService = blobService;
        _cacheService = cacheService;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<Pagination<BlindBoxDetailDto>> GetAllBlindBoxesAsync(BlindBoxQueryParameter param)
    {
        _logger.Info(
            $"[GetAllBlindBoxesAsync] Public requests blind box list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.BlindBoxes.GetQueryable()
            .Where(b => !b.IsDeleted);

        // Filter
        var keyword = param.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(b => b.Name.ToLower().Contains(keyword));

        if (param.SellerId.HasValue)
            query = query.Where(b => b.SellerId == param.SellerId.Value);

        if (param.Status.HasValue)
            query = query.Where(b => b.Status == param.Status.Value);

        if (param.MinPrice.HasValue)
            query = query.Where(b => b.Price >= param.MinPrice.Value);

        if (param.MaxPrice.HasValue)
            query = query.Where(b => b.Price <= param.MaxPrice.Value);

        if (param.ReleaseDateFrom.HasValue)
            query = query.Where(b => b.ReleaseDate >= param.ReleaseDateFrom.Value);

        if (param.ReleaseDateTo.HasValue)
            query = query.Where(b => b.ReleaseDate <= param.ReleaseDateTo.Value);

        // Sort: UpdatedAt desc, CreatedAt desc
        query = query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt);

        var count = await query.CountAsync();

        List<BlindBox> items;
        if (param.PageIndex == 0)
        {
            // Lấy BlindBox trước
            items = await query.ToListAsync();

            // Load BlindBoxItems có IsDeleted = false cho từng BlindBox
            var blindBoxIds = items.Select(b => b.Id).ToList();

            var itemsGrouped = await _unitOfWork.BlindBoxItems.GetQueryable()
                .Where(i => blindBoxIds.Contains(i.BlindBoxId) && !i.IsDeleted)
                .Include(i => i.Product)
                .ToListAsync();

            foreach (var box in items)
            {
                box.BlindBoxItems = itemsGrouped.Where(i => i.BlindBoxId == box.Id).ToList();
            }
        }
        else
        {
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

            var blindBoxIds = items.Select(b => b.Id).ToList();

            var itemsGrouped = await _unitOfWork.BlindBoxItems.GetQueryable()
                .Where(i => blindBoxIds.Contains(i.BlindBoxId) && !i.IsDeleted)
                .Include(i => i.Product)
                .ToListAsync();

            foreach (var box in items)
            {
                box.BlindBoxItems = itemsGrouped.Where(i => i.BlindBoxId == box.Id).ToList();
            }
        }

        var dtos = items.Select(b =>
        {
            var dto = _mapperService.Map<BlindBox, BlindBoxDetailDto>(b);
            dto.Items = b.BlindBoxItems.Select(item => new BlindBoxItemDto
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                ProductName = item.Product.Name,
                ImageUrl = item.Product.ImageUrls.FirstOrDefault(),
                DropRate = item.DropRate,
                Rarity = item.Rarity
            }).ToList();
            return dto;
        }).ToList();

        var result = new Pagination<BlindBoxDetailDto>(dtos, count, param.PageIndex, param.PageSize);

        _logger.Info("[GetAllBlindBoxesAsync] Blind box list loaded from DB.");
        return result;
    }

    public async Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId)
    {
        var cacheKey = $"blindbox:{blindBoxId}";
        var cached = await _cacheService.GetAsync<BlindBoxDetailDto>(cacheKey);
        if (cached != null)
        {
            _logger.Info($"[GetBlindBoxByIdAsync] Cache hit for blind box {blindBoxId}");
            return cached;
        }

        // Lấy BlindBox, không include BlindBoxItems
        var blindBox = await _unitOfWork.BlindBoxes.GetQueryable()
            .Where(x => x.Id == blindBoxId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (blindBox == null)
        {
            _logger.Warn($"[GetBlindBoxByIdAsync] Blind Box {blindBoxId} not found.");
            throw ErrorHelper.NotFound("Blind Box không tồn tại");
        }

        // Load BlindBoxItems lọc isDeleted = false
        var items = await _unitOfWork.BlindBoxItems.GetQueryable()
            .Where(i => i.BlindBoxId == blindBoxId && !i.IsDeleted)
            .Include(i => i.Product)
            .ToListAsync();

        blindBox.BlindBoxItems = items;

        var result = _mapperService.Map<BlindBox, BlindBoxDetailDto>(blindBox);
        result.Items = blindBox.BlindBoxItems.Select(item => new BlindBoxItemDto
        {
            ProductId = item.ProductId,
            ProductName = item.Product?.Name ?? string.Empty,
            DropRate = item.DropRate,
            ImageUrl = item.Product?.ImageUrls.FirstOrDefault(),
            Quantity = item.Quantity,
            Rarity = item.Rarity
        }).ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
        _logger.Info($"[GetBlindBoxByIdAsync] Blind box {blindBoxId} loaded from DB and cached.");
        return result;
    }

    public async Task<BlindBoxDetailDto> CreateBlindBoxAsync(CreateBlindBoxDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;
        _logger.Info($"[CreateBlindBoxAsync] User {currentUserId} requests to create blind box: {dto.Name}");

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

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.UserId == currentUserId && !s.IsDeleted && s.Status == SellerStatus.Approved);

        if (seller == null)
            throw ErrorHelper.Forbidden("Bạn chưa được xác minh Seller để tạo Blind Box.");

        // Upload file ảnh lên BlobStorage
        var fileName = $"blindbox-thumbnails/thumbnails-{Guid.NewGuid()}{Path.GetExtension(dto.ImageFile.FileName)}";
        await using var stream = dto.ImageFile.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        // Lấy link file đã upload
        var imageUrl = await _blobService.GetPreviewUrlAsync(fileName);
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

        var result = await _unitOfWork.BlindBoxes.AddAsync(blindBox);
        await _unitOfWork.SaveChangesAsync();

        _logger.Success($"[CreateBlindBoxAsync] Blind box {blindBox.Name} created by user {currentUserId}.");
        var mappingResult = _mapperService.Map<BlindBox, BlindBoxDetailDto>(result);
        return mappingResult;
    }

    public async Task<BlindBoxDetailDto> AddItemsToBlindBoxAsync(Guid blindBoxId, List<BlindBoxItemDto> items)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
        {
            _logger.Warn($"[AddItemsToBlindBoxAsync] Blind Box {blindBoxId} not found.");
            throw ErrorHelper.NotFound("Blind Box không tồn tại.");
        }

        var currentUserId = _claimsService.CurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(x =>
            x.Id == blindBox.SellerId && x.UserId == currentUserId && !x.IsDeleted);

        if (seller == null)
        {
            _logger.Warn(
                $"[AddItemsToBlindBoxAsync] User {currentUserId} has no permission for Blind Box {blindBoxId}.");
            throw ErrorHelper.Forbidden("Không có quyền chỉnh sửa Blind Box này.");
        }

        // Validate số lượng item phải là 6 hoặc 12
        if (items.Count != 6 && items.Count != 12)
            throw ErrorHelper.BadRequest("Blind Box chỉ được phép chứa đúng 6 hoặc 12 sản phẩm.");

        // Validate logic (dropRate, số lượng tồn kho, secret logic)
        await ValidateBlindBoxItemsAsync(blindBox, seller, items);

        // Lấy toàn bộ product liên quan để xử lý cập nhật tồn kho
        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) &&
            p.SellerId == seller.Id &&
            !p.IsDeleted);

        // Chuẩn bị entity item + trừ stock tương ứng
        var now = _time.GetCurrentTime();
        var entities = new List<BlindBoxItem>();

        foreach (var item in items)
        {
            var product = products.First(p => p.Id == item.ProductId);

            if (item.Quantity > product.Stock)
                throw ErrorHelper.BadRequest($"Sản phẩm '{product.Name}' không đủ tồn kho.");

            // Trừ tồn kho
            product.Stock -= item.Quantity;

            entities.Add(new BlindBoxItem
            {
                Id = Guid.NewGuid(),
                BlindBoxId = blindBoxId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                DropRate = item.DropRate,
                Rarity = item.Rarity,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = currentUserId
            });
        }

        await _unitOfWork.BlindBoxItems.AddRangeAsync(entities);
        await _unitOfWork.Products.UpdateRange(products);
        await _unitOfWork.SaveChangesAsync();

        await RemoveBlindBoxCacheAsync(blindBoxId);

        
        _logger.Success(
            $"[AddItemsToBlindBoxAsync] Added {entities.Count} items to Blind Box {blindBoxId} and deducted stock.");
        return await GetBlindBoxByIdAsync(blindBoxId);
    }

    public async Task<BlindBoxDetailDto> SubmitBlindBoxAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
        {
            _logger.Warn($"[SubmitBlindBoxAsync] Blind Box {blindBoxId} not found.");
            throw ErrorHelper.NotFound("Blind Box không tồn tại");
        }

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

        
        await RemoveBlindBoxCacheAsync(blindBoxId);
        
        _logger.Success($"[SubmitBlindBoxAsync] Blind Box {blindBoxId} submitted for approval.");
        return await GetBlindBoxByIdAsync(blindBox.Id);
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
                ProductName = i.Product.Name,
                ImageUrl = i.Product.ImageUrls.FirstOrDefault()
            }).ToList();

            return dto;
        }).ToList();
    }

    public async Task<BlindBoxDetailDto> ReviewBlindBoxAsync(Guid blindBoxId, bool approve, string? rejectReason = null)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            b => b.Id == blindBoxId && b.Status == BlindBoxStatus.PendingApproval && !b.IsDeleted,
            b => b.BlindBoxItems,
            b => b.Seller
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound("Blind Box không tồn tại hoặc không ở trạng thái chờ duyệt.");

        var currentUserId = _claimsService.CurrentUserId;
        var now = _time.GetCurrentTime();

        if (approve)
        {
            if (blindBox.BlindBoxItems == null || !blindBox.BlindBoxItems.Any())
                throw ErrorHelper.BadRequest("Blind Box không chứa item nào.");

            var totalDropRate = blindBox.BlindBoxItems.Sum(i => i.DropRate);
            if (totalDropRate != 100)
                throw ErrorHelper.BadRequest("Tổng DropRate phải bằng 100%.");

            blindBox.Status = BlindBoxStatus.Approved;
            blindBox.UpdatedAt = now;
            blindBox.UpdatedBy = currentUserId;

            await _unitOfWork.BlindBoxes.Update(blindBox);

            var configs = blindBox.BlindBoxItems.Select(item => new ProbabilityConfig
            {
                Id = Guid.NewGuid(),
                BlindBoxItemId = item.Id,
                Probability = item.DropRate,
                EffectiveFrom = now,
                EffectiveTo = blindBox.ReleaseDate,
                ApprovedBy = currentUserId,
                ApprovedAt = now,
                CreatedAt = now,
                CreatedBy = currentUserId
            }).ToList();

            await _unitOfWork.ProbabilityConfigs.AddRangeAsync(configs);

            var sellerUser =
                await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == blindBox.Seller.UserId && !u.IsDeleted);
            if (sellerUser != null)
                await _emailService.SendBlindBoxApprovedAsync(
                    sellerUser.Email,
                    sellerUser.FullName ?? "Seller",
                    blindBox.Name
                );
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
                throw ErrorHelper.BadRequest("Lý do từ chối là bắt buộc.");

            blindBox.Status = BlindBoxStatus.Rejected;
            blindBox.RejectReason = rejectReason.Trim();
            blindBox.UpdatedAt = now;
            blindBox.UpdatedBy = currentUserId;

            await _unitOfWork.BlindBoxes.Update(blindBox);

            var sellerUser =
                await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == blindBox.Seller.UserId && !u.IsDeleted);
            if (sellerUser != null)
                await _emailService.SendBlindBoxRejectedAsync(
                    sellerUser.Email,
                    sellerUser.FullName ?? "Seller",
                    blindBox.Name,
                    rejectReason
                );
        }

        await _unitOfWork.SaveChangesAsync();
        return await GetBlindBoxByIdAsync(blindBox.Id);
    }


    public async Task<BlindBoxDetailDto> RemoveItemFromBlindBoxAsync(Guid itemId)
    {
        var item = await _unitOfWork.BlindBoxItems.FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted,
            i => i.Product, i => i.BlindBox);

        if (item == null)
            throw ErrorHelper.NotFound("Item không tồn tại hoặc đã bị xoá.");

        var blindBox = item.BlindBox;
        if (blindBox == null || blindBox.IsDeleted)
            throw ErrorHelper.NotFound("Blind Box không hợp lệ.");

        var currentUserId = _claimsService.CurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.Id == blindBox.SellerId && s.UserId == currentUserId && !s.IsDeleted);

        if (seller == null)
            throw ErrorHelper.Forbidden("Không có quyền xoá item khỏi Blind Box này.");

        // Hoàn lại tồn kho
        var product = item.Product;
        product.Stock += item.Quantity;
        await _unitOfWork.Products.Update(product);

        await _unitOfWork.BlindBoxItems.SoftRemove(item);
        await _unitOfWork.SaveChangesAsync();

        _logger.Success($"[RemoveItemFromBlindBoxAsync] Đã xoá item {itemId} khỏi Blind Box {blindBox.Id}.");

        return await GetBlindBoxByIdAsync(blindBox.Id);
    }

    public async Task<BlindBoxDetailDto> ClearItemsFromBlindBoxAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            b => b.Id == blindBoxId && !b.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound("Blind Box không tồn tại.");

        var currentUserId = _claimsService.CurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.Id == blindBox.SellerId && s.UserId == currentUserId && !s.IsDeleted);

        if (seller == null)
            throw ErrorHelper.Forbidden("Không có quyền xoá item khỏi Blind Box này.");

        if (!blindBox.BlindBoxItems.Any())
            return await GetBlindBoxByIdAsync(blindBoxId);

        var items = blindBox.BlindBoxItems.ToList();

        // Hoàn lại stock
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) && !p.IsDeleted && p.SellerId == seller.Id);

        foreach (var item in items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
                product.Stock += item.Quantity;
        }

        await _unitOfWork.Products.UpdateRange(products);
        await _unitOfWork.BlindBoxItems.SoftRemoveRange(items);
        await _unitOfWork.SaveChangesAsync();

        await RemoveBlindBoxCacheAsync(blindBoxId);
        _logger.Success($"[ClearItemsFromBlindBoxAsync] Đã xoá toàn bộ item khỏi Blind Box {blindBoxId}.");

        return await GetBlindBoxByIdAsync(blindBoxId);
    }


    /// <summary>
    ///     1. Danh sách item không được để trống.
    ///     2. Mỗi sản phẩm phải thuộc Seller hiện tại và còn hàng (Stock > 0, chưa bị xoá).
    ///     3. Số lượng (quantity) của mỗi item không được vượt quá tồn kho (Stock) của sản phẩm tương ứng.
    ///     4. Blind Box phải có ít nhất 1 item loại Secret.
    ///     5. Nếu có item loại Secret thì DropRate cố định là 5% (frontend không được nhập).
    ///     6. Nếu Blind Box không hỗ trợ Secret nhưng có item Secret thì sẽ báo lỗi.
    ///     7. Tổng DropRate của các item (trừ Secret) phải nhỏ hơn 100%.
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
        var hasSecret = false;

        foreach (var item in items)
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

        if (!hasSecret)
            throw ErrorHelper.BadRequest("Blind Box phải có ít nhất 1 item loại Secret.");

        if (totalDropRate >= 100)
            throw ErrorHelper.BadRequest("Tổng DropRate của item (trừ Secret) phải nhỏ hơn 100%.");
    }


    /// <summary>
    ///     Xóa cache liên quan đến BlindBox (theo id và theo list).
    /// </summary>
    private async Task RemoveBlindBoxCacheAsync(Guid blindBoxId)
    {
        await _cacheService.RemoveAsync($"blindbox:{blindBoxId}");
        await _cacheService.RemoveByPatternAsync("blindbox:all");
    }
}