using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
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
    private readonly ICategoryService _categoryService;
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
        ILoggerService logger, IEmailService emailService, ICategoryService categoryService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _time = time;
        _mapperService = mapperService;
        _blobService = blobService;
        _cacheService = cacheService;
        _logger = logger;
        _emailService = emailService;
        _categoryService = categoryService;
    }

    public async Task<Pagination<BlindBoxDetailDto>> GetAllBlindBoxesAsync(BlindBoxQueryParameter param)
    {
        _logger.Info(
            $"[GetAllBlindBoxesAsync] Public requests blind box list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.BlindBoxes.GetQueryable()
            .Include(s => s.Seller)
            .Where(b => !b.IsDeleted);

        var cacheKey = BlindBoxCacheKeys.BlindBoxAll(JsonSerializer.Serialize(param));
        var cached = await _cacheService.GetAsync<Pagination<BlindBoxDetailDto>>(cacheKey);
        if (cached != null)
            return cached;

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

        if (param.CategoryId.HasValue)
        {
            var categoryIds = await _categoryService.GetAllChildCategoryIdsAsync(param.CategoryId.Value);
            query = query.Where(b => categoryIds.Contains(b.CategoryId));
        }

        if (param.HasItem == true)
        {
            var boxIdsWithItem = _unitOfWork.BlindBoxItems.GetQueryable()
                .Where(i => !i.IsDeleted)
                .Select(i => i.BlindBoxId)
                .Distinct();

            query = query.Where(b => boxIdsWithItem.Contains(b.Id));
        }


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

            foreach (var box in items) box.BlindBoxItems = itemsGrouped.Where(i => i.BlindBoxId == box.Id).ToList();
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

            foreach (var box in items) box.BlindBoxItems = itemsGrouped.Where(i => i.BlindBoxId == box.Id).ToList();
        }

        var dtos = new List<BlindBoxDetailDto>();
        foreach (var b in items)
        {
            var dto = await MapBlindBoxToDtoAsync(b);
            dtos.Add(dto);
        }

        var result = new Pagination<BlindBoxDetailDto>(dtos, count, param.PageIndex, param.PageSize);

        _logger.Info("[GetAllBlindBoxesAsync] Blind box list loaded from DB.");
        return result;
    }

    public async Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId)
    {
        var cacheKey = BlindBoxCacheKeys.BlindBoxDetail(blindBoxId);
        var cached = await _cacheService.GetAsync<BlindBoxDetailDto>(cacheKey);
        if (cached != null)
        {
            _logger.Info($"[GetBlindBoxByIdAsync] Cache hit for blind box {blindBoxId}");
            return cached;
        }

        // Lấy BlindBox, không include BlindBoxItems
        var blindBox = await _unitOfWork.BlindBoxes.GetQueryable()
            .Include(b => b.Seller)
            .Where(x => x.Id == blindBoxId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (blindBox == null)
        {
            _logger.Warn($"[GetBlindBoxByIdAsync] Blind Box {blindBoxId} not found.");
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);
        }

        // Load BlindBoxItems lọc isDeleted = false
        var items = await _unitOfWork.BlindBoxItems.GetQueryable()
            .Where(i => i.BlindBoxId == blindBoxId && !i.IsDeleted)
            .Include(i => i.Product)
            .ToListAsync();

        blindBox.BlindBoxItems = items;

        var result = await MapBlindBoxToDtoAsync(blindBox);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
        _logger.Info($"[GetBlindBoxByIdAsync] Blind box {blindBoxId} loaded from DB and cached.");
        return result;
    }

    public async Task<BlindBoxDetailDto> CreateBlindBoxAsync(CreateBlindBoxDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;
        _logger.Info($"[CreateBlindBoxAsync] User {currentUserId} requests to create blind box: {dto.Name}");

        if (dto == null)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxDataRequired);

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxNameRequired);

        if (dto.Price <= 0)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxPriceInvalid);

        if (dto.TotalQuantity <= 0)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxTotalQuantityInvalid);

        if (dto.ReleaseDate == default)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxReleaseDateInvalid);

        if (dto.ImageFile == null || dto.ImageFile.Length == 0)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxImageRequired);

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.UserId == currentUserId && !s.IsDeleted && s.Status == SellerStatus.Approved);

        if (seller == null)
            throw ErrorHelper.Forbidden(ErrorMessages.BlindBoxSellerNotVerified);

        // Kiểm tra Category tồn tại và là danh mục lá
        await ValidateLeafCategoryAsync(dto.CategoryId);


        var fileName = $"blindbox-thumbnails/thumbnails-{Guid.NewGuid()}{Path.GetExtension(dto.ImageFile.FileName)}";
        await using var stream = dto.ImageFile.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var imageUrl = await _blobService.GetPreviewUrlAsync(fileName);
        if (string.IsNullOrEmpty(imageUrl))
            throw ErrorHelper.Internal(ErrorMessages.BlindBoxImageUrlError);

        var releaseDateUtc = DateTime.SpecifyKind(dto.ReleaseDate, DateTimeKind.Utc);

        var blindBox = new BlindBox
        {
            Id = Guid.NewGuid(),
            SellerId = seller.Id,
            CategoryId = dto.CategoryId,
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

        _logger.Success($"[CreateBlindBoxAsync] Blind box {blindBox.Name} created by user {currentUserId}.");
        await RemoveBlindBoxCacheAsync(blindBox.Id, seller.Id);
        return await GetBlindBoxByIdAsync(blindBox.Id);
    }

    public async Task<BlindBoxDetailDto> UpdateBlindBoxAsync(Guid blindBoxId, UpdateBlindBoxDto dto)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(b => b.Id == blindBoxId && !b.IsDeleted);
        if (blindBox == null)
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);

        var currentUserId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.Id == blindBox.SellerId && s.UserId == currentUserId && !s.IsDeleted);
        if (seller == null)
            throw ErrorHelper.Forbidden(ErrorMessages.BlindBoxNoUpdatePermission);

        if (!string.IsNullOrWhiteSpace(dto.Name))
            blindBox.Name = dto.Name.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Description))
            blindBox.Description = dto.Description.Trim();

        if (dto.Price.HasValue)
            blindBox.Price = dto.Price.Value;

        if (dto.TotalQuantity.HasValue)
            blindBox.TotalQuantity = dto.TotalQuantity.Value;

        if (dto.ReleaseDate.HasValue)
            blindBox.ReleaseDate = DateTime.SpecifyKind(dto.ReleaseDate.Value, DateTimeKind.Utc);

        if (dto.HasSecretItem.HasValue)
            blindBox.HasSecretItem = dto.HasSecretItem.Value;

        if (dto.SecretProbability.HasValue)
            blindBox.SecretProbability = dto.SecretProbability.Value;

        if (dto.CategoryId != null)
        {
            await ValidateLeafCategoryAsync(dto.CategoryId.Value);
            blindBox.CategoryId = dto.CategoryId.Value;
        }

        if (dto.ImageFile != null)
            try
            {
                blindBox.ImageUrl = await _blobService.ReplaceImageAsync(
                    dto.ImageFile.OpenReadStream(),
                    dto.ImageFile.FileName,
                    blindBox.ImageUrl,
                    "blindbox-thumbnails"
                );
            }
            catch (Exception ex)
            {
                _logger.Error($"[UpdateBlindBoxAsync] ReplaceImageAsync failed: {ex.Message}");
                throw ErrorHelper.Internal(ErrorMessages.BlindBoxImageUpdateError);
            }

        blindBox.UpdatedAt = _time.GetCurrentTime();
        blindBox.UpdatedBy = currentUserId;

        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();
        await RemoveBlindBoxCacheAsync(blindBoxId);

        _logger.Success($"[UpdateBlindBoxAsync] Cập nhật Blind Box {blindBoxId} thành công.");
        return await GetBlindBoxByIdAsync(blindBoxId);
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
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);
        }

        var currentUserId = _claimsService.CurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(x =>
            x.Id == blindBox.SellerId && x.UserId == currentUserId && !x.IsDeleted);

        if (seller == null)
        {
            _logger.Warn(
                $"[AddItemsToBlindBoxAsync] User {currentUserId} has no permission for Blind Box {blindBoxId}.");
            throw ErrorHelper.Forbidden(ErrorMessages.BlindBoxNoEditPermission);
        }

        // Validate số lượng item phải là 6 hoặc 12
        //if (items.Count != 6 && items.Count != 12)
        if (items.Count <= 0)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxItemCountInvalid);

        // Validate sản phẩm cùng root category
        await ValidateSameRootCategoryAsync(items.Select(i => i.ProductId).ToList());

        // Validate logic (dropRate, số lượng tồn kho, secret logic)
        await ValidateBlindBoxItemsAsync(blindBox, seller, items);

        // Lấy toàn bộ product liên quan để xử lý cập nhật tồn kho
        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) &&
            p.SellerId == seller.Id &&
            !p.IsDeleted, x => x.Category);

        // --- NEW: Collect unique category names for tags ---
        var categoryNames = products
            .Where(p => p.Category != null)
            .Select(p => p.Category.Name)
            .Distinct()
            .ToList();

        // Store as JSON array
        blindBox.BindBoxTags = JsonSerializer.Serialize(categoryNames);

        // Chuẩn bị entity item + trừ stock tương ứng
        var now = _time.GetCurrentTime();
        var entities = new List<BlindBoxItem>();

        foreach (var item in items)
        {
            var product = products.First(p => p.Id == item.ProductId);

            if (item.Quantity > product.Stock)
                throw ErrorHelper.BadRequest(string.Format(ErrorMessages.BlindBoxProductStockExceeded, product.Name));

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

        await RemoveBlindBoxCacheAsync(blindBoxId, seller?.Id);


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
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);
        }

        if (blindBox.BlindBoxItems == null || !blindBox.BlindBoxItems.Any())
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxAtLeastOneItem);

        var totalDropRate = blindBox.BlindBoxItems.Sum(i => i.DropRate);
        if (totalDropRate != 100)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxDropRateMustBe100);

        var itemCount = blindBox.BlindBoxItems.Count;
        if (itemCount != 6 && itemCount != 12)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxItemCountInvalid);

        blindBox.UpdatedAt = _time.GetCurrentTime();
        blindBox.Status = BlindBoxStatus.PendingApproval;

        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();


        await RemoveBlindBoxCacheAsync(blindBoxId);

        _logger.Success($"[SubmitBlindBoxAsync] Blind Box {blindBoxId} submitted for approval.");
        return await GetBlindBoxByIdAsync(blindBox.Id);
    }

    public async Task<BlindBoxDetailDto> ReviewBlindBoxAsync(Guid blindBoxId, bool approve, string? rejectReason = null)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            b => b.Id == blindBoxId && b.Status == BlindBoxStatus.PendingApproval && !b.IsDeleted,
            b => b.BlindBoxItems,
            b => b.Seller
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFoundOrNotPending);

        var currentUserId = _claimsService.CurrentUserId;
        var now = _time.GetCurrentTime();

        if (approve)
        {
            if (blindBox.BlindBoxItems == null || !blindBox.BlindBoxItems.Any())
                throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxNoItems);

            var totalDropRate = blindBox.BlindBoxItems.Sum(i => i.DropRate);
            if (totalDropRate != 100)
                throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxDropRateMustBe100);

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
                throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxRejectReasonRequired);

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

    public async Task<BlindBoxDetailDto> ClearItemsFromBlindBoxAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            b => b.Id == blindBoxId && !b.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);

        var currentUserId = _claimsService.CurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.Id == blindBox.SellerId && s.UserId == currentUserId && !s.IsDeleted);

        if (seller == null)
            throw ErrorHelper.Forbidden(ErrorMessages.BlindBoxNoDeleteItemPermission);

        if (blindBox.BlindBoxItems != null && !blindBox.BlindBoxItems.Any())
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

    public async Task<BlindBoxDetailDto> DeleteBlindBoxAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            b => b.Id == blindBoxId && !b.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);

        var currentUserId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.Id == blindBox.SellerId && s.UserId == currentUserId && !s.IsDeleted);

        if (seller == null)
            throw ErrorHelper.Forbidden(ErrorMessages.BlindBoxNoDeletePermission);

        // Soft delete BlindBox
        await _unitOfWork.BlindBoxes.SoftRemove(blindBox);

        // Restore stock của từng item
        var productIds = blindBox.BlindBoxItems.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) && p.SellerId == seller.Id && !p.IsDeleted);

        foreach (var item in blindBox.BlindBoxItems)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
                product.Stock += item.Quantity;
        }

        await _unitOfWork.Products.UpdateRange(products);
        await _unitOfWork.SaveChangesAsync();

        await RemoveBlindBoxCacheAsync(blindBoxId);

        _logger.Success($"[DeleteBlindBoxAsync] Đã xoá Blind Box {blindBoxId}.");

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

        return result;
    }

    #region private methods

    private async Task ValidateBlindBoxItemsAsync(BlindBox blindBox, Seller seller, List<BlindBoxItemDto> items)
    {
        if (items == null || items.Count == 0)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxItemListRequired);

        var productIds = items.Select(i => i.ProductId).ToList();

        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) &&
            p.SellerId == seller.Id &&
            p.Stock > 0 &&
            !p.IsDeleted);

        if (products.Count != items.Count)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxProductInvalidOrOutOfStock);

        // Validate số lượng tồn kho
        foreach (var item in items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            if (item.Quantity > product.Stock)
                throw ErrorHelper.BadRequest(string.Format(ErrorMessages.BlindBoxProductStockExceeded, product.Name));
        }

        // Tách 2 nhóm: Secret và Non-Secret
        var secretItems = items.Where(i => i.Rarity == BlindBoxRarity.Secret).ToList();
        var normalItems = items.Where(i => i.Rarity != BlindBoxRarity.Secret).ToList();

        if (!blindBox.HasSecretItem && secretItems.Count > 0)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxNoSecretSupport);

        if (blindBox.HasSecretItem && secretItems.Count == 0)
            throw ErrorHelper.BadRequest(ErrorMessages.BlindBoxSecretItemRequired);

        // Tổng DropRate
        decimal totalDropRate = 0;

        foreach (var item in normalItems)
        {
            if (item.DropRate <= 0)
                throw ErrorHelper.BadRequest($"Sản phẩm '{item.ProductName}' phải có DropRate > 0.");
            totalDropRate += item.DropRate;
        }

        foreach (var item in secretItems)
        {
            item.DropRate = blindBox.SecretProbability; // ép đúng tỉ lệ đã cấu hình từ DTO
            totalDropRate += item.DropRate;
        }

        if (Math.Round(totalDropRate, 2) != 100m)
            throw ErrorHelper.BadRequest($"Tổng DropRate phải đúng bằng 100%. Hiện tại: {totalDropRate}%");
    }
    private async Task ValidateSameRootCategoryAsync(List<Guid> productIds)
    {
        var products = await _unitOfWork.Products.GetQueryable()
            .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
            .Include(p => p.Category)
            .ThenInclude(c => c.Parent)
            .ToListAsync();

        Guid GetRootId(Category category)
        {
            while (category.Parent != null)
                category = category.Parent;
            return category.Id;
        }

        var distinctRootIds = products
            .Select(p => GetRootId(p.Category))
            .Distinct()
            .ToList();

        if (distinctRootIds.Count > 1)
            throw ErrorHelper.BadRequest("Tất cả sản phẩm trong blind box phải cùng loại (cùng root category).");
    }
    private async Task ValidateLeafCategoryAsync(Guid categoryId)
    {
        var category = await _categoryService.GetWithParentAsync(categoryId);
        if (category == null)
            throw ErrorHelper.BadRequest(ErrorMessages.CategoryNotFound);

        var hasChild = await _unitOfWork.Categories.GetQueryable()
            .AnyAsync(c => c.ParentId == categoryId && !c.IsDeleted);

        if (hasChild)
            throw ErrorHelper.BadRequest(ErrorMessages.CategoryChildrenError);
    }
    private async Task RemoveBlindBoxCacheAsync(Guid blindBoxId, Guid? sellerId = null)
    {
        await _cacheService.RemoveAsync(BlindBoxCacheKeys.BlindBoxDetail(blindBoxId));
        await _cacheService.RemoveByPatternAsync(BlindBoxCacheKeys.BlindBoxAllPrefix + "*");

        if (sellerId.HasValue)
            await _cacheService.RemoveAsync(BlindBoxCacheKeys.BlindBoxSeller(sellerId.Value));
    }
    private static class BlindBoxCacheKeys
    {
        public const string BlindBoxAllPrefix = "blindbox:list:public";

        public static string BlindBoxDetail(Guid id)
        {
            return $"blindbox:detail:{id}";
        }

        public static string BlindBoxSeller(Guid sellerId)
        {
            return $"blindbox:list:seller:{sellerId}";
        }

        public static string BlindBoxAll(string paramJson)
        {
            return $"{BlindBoxAllPrefix}:{paramJson}";
        }
    }
    private Task<BlindBoxDetailDto> MapBlindBoxToDtoAsync(BlindBox blindBox)
    {
        var dto = _mapperService.Map<BlindBox, BlindBoxDetailDto>(blindBox);
        dto.BlindBoxStockStatus = blindBox.TotalQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock;
        dto.Brand = blindBox.Seller?.CompanyName;

        // Gán danh sách item
        dto.Items = blindBox.BlindBoxItems.Select(item => new BlindBoxItemDto
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            ProductName = item.Product?.Name ?? string.Empty,
            ImageUrl = item.Product?.ImageUrls.FirstOrDefault(),
            DropRate = item.DropRate,
            Rarity = item.Rarity
        }).ToList();
        return Task.FromResult(dto);
    }

    #endregion
}