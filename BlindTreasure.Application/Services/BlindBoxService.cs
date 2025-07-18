﻿using System.Text.Json;
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
    private readonly INotificationService _notificationService;
    private readonly ICurrentTime _time;
    private readonly IUnitOfWork _unitOfWork;

    public BlindBoxService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime time,
        IMapperService mapperService,
        IBlobService blobService,
        ICacheService cacheService,
        ILoggerService logger, IEmailService emailService, ICategoryService categoryService,
        INotificationService notificationService)
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
        _notificationService = notificationService;
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


        // Sort: UpdatedAt/CreatedAt theo hướng param.Desc
        if (param.Desc)
            query = query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt);
        else
            query = query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);

        var count = await query.CountAsync();

        List<BlindBox> items;
        if (param.PageIndex == 0)
        {
            items = await query.ToListAsync();
            await LoadBlindBoxItemsAsync(items, true);
        }
        else
        {
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();
            await LoadBlindBoxItemsAsync(items);
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

        // 1. Lấy BlindBox (không include navigation sâu)
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(b => b.Id == blindBoxId && !b.IsDeleted
        );

        if (blindBox == null)
        {
            _logger.Warn($"[GetBlindBoxByIdAsync] Blind Box {blindBoxId} not found.");
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);
        }

        // 2. Lấy danh sách BlindBoxItems kèm Product và RarityConfig
        var items = await _unitOfWork.BlindBoxItems.GetQueryable()
            .Where(i => i.BlindBoxId == blindBoxId && !i.IsDeleted)
            .Include(i => i.Product)
            .Include(i => i.RarityConfig)
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
        {
            blindBox.TotalQuantity = dto.TotalQuantity.Value;

            // Cập nhật status dựa trên số lượng
            if (dto.TotalQuantity.Value <= 0 && blindBox.Status == BlindBoxStatus.Approved)
            {
                blindBox.Status = BlindBoxStatus.Rejected; // Hoặc enum OutOfStock nếu có
                _logger.Info(
                    $"[UpdateBlindBoxAsync] BlindBox {blindBoxId} đã hết hàng, cập nhật status thành Rejected");
            }
        }

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

    public async Task<BlindBoxDetailDto> AddItemsToBlindBoxAsync(Guid blindBoxId, List<BlindBoxItemRequestDto> items)
    {
        if (items == null || items.Count == 0)
            throw ErrorHelper.BadRequest("Blind Box cần có ít nhất 1 sản phẩm.");

        if (items.Count != 6 && items.Count != 12)
            throw ErrorHelper.BadRequest("Mỗi Blind Box chỉ được chứa 6 hoặc 12 sản phẩm.");

        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems
        );
        if (blindBox == null)
            throw ErrorHelper.NotFound("Không tìm thấy Blind Box.");

        var currentUserId = _claimsService.CurrentUserId;

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(x =>
            x.Id == blindBox.SellerId && x.UserId == currentUserId && !x.IsDeleted);

        if (seller == null)
            throw ErrorHelper.Forbidden("Bạn không có quyền chỉnh sửa Blind Box này.");

        ValidateBlindBoxItemsFullRule(items);

        var products = await _unitOfWork.Products.GetAllAsync(p =>
            items.Select(i => i.ProductId).Contains(p.Id) && p.SellerId == seller.Id && !p.IsDeleted);

        var dropRates = CalculateDropRates(items);
        var now = _time.GetCurrentTime();

        if (blindBox.BlindBoxItems == null)
            blindBox.BlindBoxItems = new List<BlindBoxItem>();

        var existingItems = blindBox.BlindBoxItems.Where(i => !i.IsDeleted).ToList();
        var newBlindBoxItems = new List<BlindBoxItem>();
        var newRarityConfigs = new List<RarityConfig>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null)
                throw ErrorHelper.BadRequest($"Sản phẩm tại vị trí {i + 1} không hợp lệ.");
            if (item.Quantity > product.Stock)
                throw ErrorHelper.BadRequest($"Sản phẩm '{product.Name}' không đủ tồn kho.");

            var dropRate = dropRates[item];

            if (i < existingItems.Count)
            {
                var existing = existingItems[i];
                existing.ProductId = item.ProductId;
                existing.Quantity = item.Quantity;
                existing.DropRate = dropRate;
                existing.IsSecret = item.Rarity == RarityName.Secret;
                existing.IsActive = true;
                existing.UpdatedAt = now;
                existing.UpdatedBy = currentUserId;

                await _unitOfWork.BlindBoxItems.Update(existing);

                var rarity = await _unitOfWork.RarityConfigs.FirstOrDefaultAsync(r =>
                    r.BlindBoxItemId == existing.Id && !r.IsDeleted);

                if (rarity != null)
                {
                    rarity.Name = item.Rarity;
                    rarity.Weight = item.Weight;
                    rarity.IsSecret = item.Rarity == RarityName.Secret;
                    rarity.UpdatedAt = now;
                    rarity.UpdatedBy = currentUserId;
                    await _unitOfWork.RarityConfigs.Update(rarity);
                }
            }
            else
            {
                var newItem = new BlindBoxItem
                {
                    Id = Guid.NewGuid(),
                    BlindBoxId = blindBoxId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    DropRate = dropRate,
                    IsSecret = item.Rarity == RarityName.Secret,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                };
                newBlindBoxItems.Add(newItem);

                newRarityConfigs.Add(new RarityConfig
                {
                    Id = Guid.NewGuid(),
                    BlindBoxItemId = newItem.Id,
                    Name = item.Rarity,
                    Weight = item.Weight,
                    IsSecret = item.Rarity == RarityName.Secret,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                });
            }
        }

        if (newBlindBoxItems.Any())
            await _unitOfWork.BlindBoxItems.AddRangeAsync(newBlindBoxItems);
        if (newRarityConfigs.Any())
            await _unitOfWork.RarityConfigs.AddRangeAsync(newRarityConfigs);

        blindBox.HasSecretItem = items.Any(i => i.Rarity == RarityName.Secret);
        blindBox.SecretProbability = dropRates
            .Where(kv => kv.Key.Rarity == RarityName.Secret)
            .Sum(kv => kv.Value);

        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();

        await RemoveBlindBoxCacheAsync(blindBoxId, seller.Id);

        return await GetBlindBoxByIdAsync(blindBoxId);
    }


    public async Task<BlindBoxDetailDto> SubmitBlindBoxAsync(Guid blindBoxId)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems
        );

        if (blindBox == null)
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);

        blindBox.UpdatedAt = _time.GetCurrentTime();
        blindBox.Status = BlindBoxStatus.PendingApproval;

        // Trừ stock thực tế từ Product cho từng item
        var productIds = blindBox.BlindBoxItems.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.GetAllAsync(p => productIds.Contains(p.Id) && !p.IsDeleted);

        foreach (var item in blindBox.BlindBoxItems)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null)
                throw ErrorHelper.BadRequest("Không tìm thấy sản phẩm cho item trong BlindBox.");

            if (item.Quantity > product.Stock)
                throw ErrorHelper.BadRequest($"Sản phẩm '{product.Name}' không đủ tồn kho để submit BlindBox.");

            product.Stock -= item.Quantity;

            // Cập nhật status của product nếu stock = 0
            if (product.Stock == 0 && product.Status != ProductStatus.InActive)
                product.Status = ProductStatus.OutOfStock;
        }

        await _unitOfWork.Products.UpdateRange(products);
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

            // --- Hoàn lại stock cho từng item ---
            var productIds = blindBox.BlindBoxItems.Select(i => i.ProductId).Distinct().ToList();
            var products = await _unitOfWork.Products.GetAllAsync(p =>
                productIds.Contains(p.Id) && !p.IsDeleted && p.SellerId == blindBox.Seller.Id);

            foreach (var item in blindBox.BlindBoxItems)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                    product.Stock += item.Quantity;
            }

            await _unitOfWork.Products.UpdateRange(products);

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

        await _notificationService.PushNotificationToUser(
            blindBox.Seller.UserId,
            new NotificationDTO
            {
                Title = approve ? "Blind Box được duyệt" : "Blind Box bị từ chối",
                Message = approve
                    ? $"Blind Box \"{blindBox.Name}\" của bạn đã được duyệt thành công."
                    : $"Blind Box \"{blindBox.Name}\" đã bị từ chối. Lý do: {rejectReason}",
                Type = NotificationType.System
            }
        );


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
        result.Items = blindBox.BlindBoxItems.Select(item => new BlindBoxItemResponseDto
        {
            ProductId = item.ProductId,
            ProductName = item.Product?.Name ?? string.Empty,
            DropRate = item.DropRate,
            ImageUrl = item.Product?.ImageUrls.FirstOrDefault(),
            Quantity = item.Quantity
        }).ToList();

        return result;
    }

    #region private methods

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

    private void ValidateBlindBoxItemsFullRule(List<BlindBoxItemRequestDto> items)
    {
        if (items.Count != 6 && items.Count != 12)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Số lượng item không hợp lệ [ActualCount={items.Count}]. Blind Box phải có đúng 6 hoặc 12 sản phẩm.");
            throw ErrorHelper.BadRequest("Blind Box phải có đúng 6 hoặc 12 sản phẩm.");
        }

        var countSecret = items.Count(i => i.Rarity == RarityName.Secret);
        if (countSecret < 1)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Không có item Secret [SecretCount={countSecret}]. Blind Box phải có ít nhất 1 item Secret.");
            throw ErrorHelper.BadRequest("Blind Box phải có ít nhất 1 item Secret.");
        }

        if (countSecret > 1)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Có nhiều hơn 1 item Secret [SecretCount={countSecret}]. Mỗi BlindBox chỉ được phép có nhiều nhất 1 item Secret.");
            throw ErrorHelper.BadRequest("Mỗi BlindBox chỉ được phép có nhiều nhất 1 item Secret.");
        }

        var validRarities = Enum.GetValues(typeof(RarityName)).Cast<RarityName>().ToList();
        var invalids = items.Where(i => !validRarities.Contains(i.Rarity)).ToList();
        if (invalids.Any())
        {
            var invalidList = string.Join(", ", invalids.Select(i => $"{i.Rarity}"));
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Phát hiện rarity không hợp lệ [InvalidRarity={invalidList}]. Chỉ chấp nhận các rarity: Common, Rare, Epic, Secret.");
            throw ErrorHelper.BadRequest("Chỉ chấp nhận các rarity: Common, Rare, Epic, Secret.");
        }

        var totalWeight = items.Sum(i => i.Weight);
        if (totalWeight != 100)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Tổng trọng số không hợp lệ [TotalWeight={totalWeight}]. Tổng trọng số (Weight) phải đúng bằng 100.");
            throw ErrorHelper.BadRequest("Tổng trọng số (Weight) phải đúng bằng 100.");
        }

        // Validate thứ tự weight: Common >= Rare >= Epic >= Secret
        var tierOrder = new List<RarityName> { RarityName.Common, RarityName.Rare, RarityName.Epic, RarityName.Secret };
        var groupWeights = tierOrder
            .Select(tier => items.Where(i => i.Rarity == tier).Sum(i => i.Weight))
            .ToList();

        for (var i = 1; i < groupWeights.Count; i++)
            if (groupWeights[i] > 0 && groupWeights[i - 1] > 0 && groupWeights[i] > groupWeights[i - 1])
            {
                var detail = string.Join(", ",
                    tierOrder.Select((r, idx) => $"{r}={groupWeights[idx]}"));
                _logger.Warn(
                    $"[ValidateBlindBoxItemsFullRule] Tổng trọng số tier sau lớn hơn tier trước [TierOrder={detail}]. Không cho phép trọng số của tier sau lớn hơn tier trước.");
                throw ErrorHelper.BadRequest(
                    "Không cho phép trọng số của tier sau lớn hơn tier trước (Common ≥ Rare ≥ Epic ≥ Secret).");
            }
    }

    private Dictionary<BlindBoxItemRequestDto, decimal> CalculateDropRates(List<BlindBoxItemRequestDto> items)
    {
        var result = new Dictionary<BlindBoxItemRequestDto, decimal>();
        var total = items.Sum(i => i.Quantity * i.Weight);

        var temp = items.Select(i => new
        {
            Item = i,
            RawRate = (decimal)(i.Quantity * i.Weight) / total * 100m
        }).ToList();

        foreach (var t in temp) result[t.Item] = Math.Round(t.RawRate, 2, MidpointRounding.AwayFromZero);

        var diff = 100.00m - result.Values.Sum();
        if (Math.Abs(diff) >= 0.01m)
        {
            var target = diff > 0
                ? result.OrderByDescending(x => x.Value).First()
                : result.OrderBy(x => x.Value).First();

            result[target.Key] = Math.Round(target.Value + diff, 2);
        }

        return result;
    }

    private async Task ValidateLeafCategoryAsync(Guid categoryId)
    {
        var category = await _categoryService.GetWithParentAsync(categoryId);
        if (category == null)
        {
            _logger.Warn($"[ValidateLeafCategoryAsync] Lỗi: Không tìm thấy category với Id = {categoryId}.");
            throw ErrorHelper.BadRequest(ErrorMessages.CategoryNotFound);
        }

        var hasChild = await _unitOfWork.Categories.GetQueryable()
            .AnyAsync(c => c.ParentId == categoryId && !c.IsDeleted);

        if (hasChild)
        {
            _logger.Warn(
                $"[ValidateLeafCategoryAsync] Lỗi: Category Id = {categoryId} vẫn còn category con, không được chọn.");
            throw ErrorHelper.BadRequest(ErrorMessages.CategoryChildrenError);
        }
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

        // Cập nhật stock status
        dto.BlindBoxStockStatus = blindBox.TotalQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock;

        // Nếu BlindBox hết hàng nhưng status không phản ánh điều đó, cập nhật trong DB
        if (blindBox.TotalQuantity <= 0 && blindBox.Status == BlindBoxStatus.Approved)
            // Chỉ log thông báo, việc cập nhật sẽ được thực hiện ở nơi khác để tránh side effect
            _logger.Warn($"[MapBlindBoxToDtoAsync] BlindBox {blindBox.Id} đã hết hàng nhưng status vẫn là Approved");

        dto.Brand = blindBox.Seller?.CompanyName;

        // Gán danh sách item
        if (blindBox.BlindBoxItems != null) dto.Items = MapToBlindBoxItemDtos(blindBox.BlindBoxItems);

        return Task.FromResult(dto);
    }

    private List<BlindBoxItemResponseDto> MapToBlindBoxItemDtos(IEnumerable<BlindBoxItem> items)
    {
        return items.Select(item => new BlindBoxItemResponseDto
        {
            ProductId = item.ProductId,
            ProductName = item.Product?.Name ?? string.Empty,
            DropRate = item.DropRate,
            ImageUrl = item.Product?.ImageUrls?.FirstOrDefault(),
            Quantity = item.Quantity,
            Rarity = item.RarityConfig?.Name ?? default,
            Weight = item.RarityConfig?.Weight ?? 0
        }).ToList();
    }

    private async Task LoadBlindBoxItemsAsync(List<BlindBox> blindBoxes, bool includeProbabilityConfigs = false)
    {
        var blindBoxIds = blindBoxes.Select(b => b.Id).ToList();

        IQueryable<BlindBoxItem> query;

        if (includeProbabilityConfigs)
            query = _unitOfWork.BlindBoxItems.GetQueryable()
                .Where(i => blindBoxIds.Contains(i.BlindBoxId) && !i.IsDeleted)
                .Include(i => i.Product)
                .Include(i => i.RarityConfig)
                .Include(i => i.ProbabilityConfigs); // đây là ICollection
        else
            query = _unitOfWork.BlindBoxItems.GetQueryable()
                .Where(i => blindBoxIds.Contains(i.BlindBoxId) && !i.IsDeleted)
                .Include(i => i.Product)
                .Include(i => i.RarityConfig);

        var itemsGrouped = await query.ToListAsync();

        foreach (var box in blindBoxes)
            box.BlindBoxItems = itemsGrouped.Where(i => i.BlindBoxId == box.Id).ToList();
    }

    #endregion
}