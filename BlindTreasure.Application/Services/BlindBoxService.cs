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
    private readonly ICategoryService _categoryService;
    private readonly IClaimsService _claimsService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapperService;
    private readonly INotificationService _notificationService;
    private readonly ICurrentTime _time;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminService _userService;

    public BlindBoxService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime time,
        IMapperService mapperService,
        IBlobService blobService,
        ILoggerService logger, IEmailService emailService, ICategoryService categoryService,
        INotificationService notificationService, IAdminService userService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _time = time;
        _mapperService = mapperService;
        _blobService = blobService;
        _logger = logger;
        _emailService = emailService;
        _categoryService = categoryService;
        _notificationService = notificationService;
        _userService = userService;
    }

    public async Task<Pagination<BlindBoxDetailDto>> GetAllBlindBoxesAsync(BlindBoxQueryParameter param)
    {
        var query = _unitOfWork.BlindBoxes.GetQueryable()
            .Include(s => s.Seller)
            .Where(b => !b.IsDeleted);


        query = await ApplyFilters(query, param);

        query = ApplySorting(query, param);

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
        return result;
    }

    public async Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId)
    {
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
        return result;
    }

    public async Task<BlindBoxDetailDto> CreateBlindBoxAsync(CreateBlindBoxDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;

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
        return await GetBlindBoxByIdAsync(blindBox.Id);
    }

// BlindBoxService.cs - UpdateBlindBoxAsync (đã chỉnh sửa)
    public async Task<BlindBoxDetailDto> UpdateBlindBoxAsync(Guid blindBoxId, UpdateBlindBoxDto dto)
    {
        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(b => b.Id == blindBoxId && !b.IsDeleted);
        if (blindBox == null)
            throw ErrorHelper.NotFound(ErrorMessages.BlindBoxNotFound);

        // Không cho seller update sau khi được duyệt
        if (blindBox.Status == BlindBoxStatus.Approved)
            throw ErrorHelper.Forbidden("BlindBox đã được duyệt, không thể chỉnh sửa.");

        var currentUserId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
            s.Id == blindBox.SellerId && s.UserId == currentUserId && !s.IsDeleted);
        if (seller == null)
            throw ErrorHelper.Forbidden(ErrorMessages.BlindBoxNoUpdatePermission);

        // Load items để validate total quantity nếu cần
        if (blindBox.BlindBoxItems == null)
        {
            var items = await _unitOfWork.BlindBoxItems.GetQueryable()
                .Where(i => i.BlindBoxId == blindBoxId && !i.IsDeleted)
                .ToListAsync();
            blindBox.BlindBoxItems = items;
        }

        var totalItemsQuantity = blindBox.BlindBoxItems?.Where(i => !i.IsDeleted).Sum(i => i.Quantity) ?? 0;

        if (!string.IsNullOrWhiteSpace(dto.Name))
            blindBox.Name = dto.Name.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Description))
            blindBox.Description = dto.Description.Trim();

        if (dto.Price.HasValue)
            blindBox.Price = dto.Price.Value;

        if (dto.TotalQuantity.HasValue)
        {
            // Bắt buộc TotalQuantity phải bằng tổng quantity trong items
            if (totalItemsQuantity > 0 && dto.TotalQuantity.Value != totalItemsQuantity)
                throw ErrorHelper.BadRequest("Tổng số lượng BlindBox phải bằng tổng số lượng của các item.");

            blindBox.TotalQuantity = dto.TotalQuantity.Value;
        }
        else
        {
            // Nếu client không gửi TotalQuantity nhưng DB đang không khớp với tổng items => đồng bộ lại
            if (blindBox.BlindBoxItems != null && blindBox.BlindBoxItems.Any() &&
                blindBox.TotalQuantity != totalItemsQuantity)
                blindBox.TotalQuantity = totalItemsQuantity;
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

        // Khi seller sửa thì đưa về Draft để chỉnh sửa + nộp duyệt lại
        blindBox.Status = BlindBoxStatus.Draft;

        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();

        _logger.Success($"[UpdateBlindBoxAsync] Cập nhật Blind Box {blindBoxId} thành công.");
        return await GetBlindBoxByIdAsync(blindBoxId);
    }

// BlindBoxService.cs - AddItemsToBlindBoxAsync (đã chỉnh sửa)
    public async Task<BlindBoxDetailDto> AddItemsToBlindBoxAsync(Guid blindBoxId, List<BlindBoxItemRequestDto> items)
    {
        if (items == null || items.Count == 0)
            throw ErrorHelper.BadRequest("Vui lòng thêm ít nhất một sản phẩm vào Blind Box.");

        if (items.Count != 6 && items.Count != 12)
            throw ErrorHelper.BadRequest("Mỗi Blind Box phải chứa đúng 6 hoặc 12 sản phẩm.");

        var blindBox = await _unitOfWork.BlindBoxes.FirstOrDefaultAsync(
            x => x.Id == blindBoxId && !x.IsDeleted,
            b => b.BlindBoxItems
        );
        if (blindBox == null)
            throw ErrorHelper.NotFound("Không tìm thấy Blind Box được chỉ định.");

        var currentUserId = _claimsService.CurrentUserId;
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(x =>
            x.Id == blindBox.SellerId && x.UserId == currentUserId && !x.IsDeleted);
        if (seller == null)
            throw ErrorHelper.Forbidden("Rất tiếc, bạn không có quyền chỉnh sửa Blind Box này.");

        ValidateBlindBoxItemsFullRule(items);

        // Nếu chưa có items trước đó -> tạo mới toàn bộ (scenario: list == null/empty)
        var existingItems = blindBox.BlindBoxItems?.Where(i => !i.IsDeleted).ToList() ?? new List<BlindBoxItem>();
        var isEmptyBefore = !existingItems.Any();

        // Load related products for validation
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) && p.SellerId == seller.Id && !p.IsDeleted);

        // ---------- SCENARIO 1: blindBox chưa có items => tạo mới ----------
        if (isEmptyBefore)
        {
            await ValidateProductStockForBlindBoxAsync(blindBox, items);

            var dropRates = CalculateDropRates(items);
            var now = _time.GetCurrentTime();

            var newBlindBoxItems = new List<BlindBoxItem>();
            var newRarityConfigs = new List<RarityConfig>();

            foreach (var dto in items)
            {
                var product = products.FirstOrDefault(p => p.Id == dto.ProductId);
                if (product == null)
                    throw ErrorHelper.BadRequest($"Sản phẩm {dto.ProductId} không hợp lệ hoặc không thuộc về bạn.");
                if (dto.Quantity > product.TotalStockQuantity)
                    throw ErrorHelper.BadRequest($"Sản phẩm '{product.Name}' không đủ số lượng trong kho.");

                var item = new BlindBoxItem
                {
                    Id = Guid.NewGuid(),
                    BlindBoxId = blindBoxId,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    DropRate = dropRates[dto],
                    IsSecret = dto.Rarity == RarityName.Secret,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                };
                newBlindBoxItems.Add(item);

                newRarityConfigs.Add(new RarityConfig
                {
                    Id = Guid.NewGuid(),
                    BlindBoxItemId = item.Id,
                    Name = dto.Rarity,
                    Weight = dto.Weight,
                    IsSecret = dto.Rarity == RarityName.Secret,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                });
            }

            if (newBlindBoxItems.Any())
                await _unitOfWork.BlindBoxItems.AddRangeAsync(newBlindBoxItems);
            if (newRarityConfigs.Any())
                await _unitOfWork.RarityConfigs.AddRangeAsync(newRarityConfigs);

            blindBox.HasSecretItem = items.Any(i => i.Rarity == RarityName.Secret);
            blindBox.SecretProbability = dropRates
                .Where(kv => kv.Key.Rarity == RarityName.Secret)
                .Sum(kv => kv.Value);

            blindBox.TotalQuantity = newBlindBoxItems.Sum(x => x.Quantity);
            blindBox.Status = BlindBoxStatus.Draft;

            await _unitOfWork.BlindBoxes.Update(blindBox);
            await _unitOfWork.SaveChangesAsync();

            return await GetBlindBoxByIdAsync(blindBoxId);
        }

        // ---------- SCENARIO 2: blindBox đã có items ----------
        // Build lookup existing by ProductId
        var existingByProduct = existingItems.ToDictionary(e => e.ProductId, e => e);

        // If box is Approved -> only allow quantity changes. Any change to rarity/weight/drop-rate/product set -> throw.
        if (blindBox.Status == BlindBoxStatus.Approved)
        {
            // Check product set unchanged
            var existingProductIds = existingByProduct.Keys.ToHashSet();
            var requestProductIds = productIds.ToHashSet();

            var added = requestProductIds.Except(existingProductIds).ToList();
            var removed = existingProductIds.Except(requestProductIds).ToList();
            if (added.Any() || removed.Any())
                throw ErrorHelper.BadRequest(
                    "BlindBox đã được duyệt. Không được thêm hoặc xóa sản phẩm. Chỉ được phép cập nhật số lượng.");

            // Load rarity configs for existing items to compare weight/rarity
            var existingItemIds = existingItems.Select(e => e.Id).ToList();
            var rarityConfigs = await _unitOfWork.RarityConfigs.GetAllAsync(r =>
                existingItemIds.Contains(r.BlindBoxItemId) && !r.IsDeleted);

            var rarityByItemId = rarityConfigs.ToDictionary(r => r.BlindBoxItemId, r => r);

            // Detect forbidden changes and detect whether any quantity changed
            var anyQuantityChanged = false;
            foreach (var dto in items)
            {
                var existing = existingByProduct[dto.ProductId];
                // compare rarity / weight with existing rarity config
                if (!rarityByItemId.TryGetValue(existing.Id, out var rarity))
                {
                    // If no rarity config found, treat as invalid to change
                    throw ErrorHelper.BadRequest(
                        "Cấu hình độ hiếm nội bộ không hợp lệ. Không thể chỉnh sửa khi đã duyệt.");
                }

                // If requested rarity or weight differs -> forbidden
                if (dto.Rarity != rarity.Name || dto.Weight != rarity.Weight)
                    throw ErrorHelper.BadRequest(
                        "BlindBox đã được duyệt. Không được thay đổi trọng số hoặc độ hiếm. Chỉ được phép thay đổi số lượng.");

                // If product changed (productId mismatch) -> forbidden (we already checked set, but extra guard)
                if (dto.ProductId != existing.ProductId)
                    throw ErrorHelper.BadRequest("BlindBox đã được duyệt. Không được thay đổi sản phẩm.");

                if (dto.Quantity != existing.Quantity)
                    anyQuantityChanged = true;
            }

            if (!anyQuantityChanged)
            {
                // Không có thay đổi quantity -> nothing to do. Trả về detail hiện tại.
                return await GetBlindBoxByIdAsync(blindBoxId);
            }

            // Only quantity changed -> update quantity, recalc droprates and set PendingApproval
            // Build DTO list for drop rate calculation using current rarity weights
            var calcDtos = existingItems.Select(e =>
            {
                var r = rarityByItemId[e.Id];
                return new BlindBoxItemRequestDto
                {
                    ProductId = e.ProductId,
                    Quantity = items.First(i => i.ProductId == e.ProductId).Quantity,
                    Weight = r.Weight,
                    Rarity = r.Name
                };
            }).ToList();

            var newDropRates = CalculateDropRates(calcDtos);
            var now = _time.GetCurrentTime();

            // Update quantities and droprates
            var productsToUpdate = new List<Product>();
            foreach (var dto in calcDtos)
            {
                var existing = existingByProduct[dto.ProductId];

                // Validate stock delta vs product available
                var product = products.FirstOrDefault(p => p.Id == dto.ProductId);
                if (product == null)
                    throw ErrorHelper.BadRequest($"Sản phẩm {dto.ProductId} không tồn tại hoặc không thuộc bạn.");

                var prevQty = existing.Quantity;
                var newQty = dto.Quantity;
                var delta = newQty - prevQty;
                if (delta > 0)
                {
                    if (product.AvailableToSell < delta)
                        throw ErrorHelper.BadRequest(
                            $"Sản phẩm '{product.Name}' không đủ số lượng khả dụng để tăng thêm {delta} đơn vị.");
                }

                // If product has reservation fields, adjust reserved accordingly
                product.ReservedInBlindBox += delta;
                productsToUpdate.Add(product);

                existing.Quantity = newQty;
                existing.DropRate = newDropRates.First(kv => kv.Key.ProductId == dto.ProductId).Value;
                existing.UpdatedAt = now;
                existing.UpdatedBy = currentUserId;
                await _unitOfWork.BlindBoxItems.Update(existing);
            }

            // Sync blindbox total quantity
            blindBox.TotalQuantity = existingItems.Sum(x => x.Quantity);
            blindBox.UpdatedAt = now;
            blindBox.UpdatedBy = currentUserId;

            // Important: Changing quantity requires re-approval
            blindBox.Status = BlindBoxStatus.PendingApproval;

            if (productsToUpdate.Any())
                await _unitOfWork.Products.UpdateRange(productsToUpdate);

            await _unitOfWork.BlindBoxes.Update(blindBox);
            await _unitOfWork.SaveChangesAsync();

            return await GetBlindBoxByIdAsync(blindBoxId);
        }

        // ---------- Else: box is Draft (or other editable status) ----------
        // Allow free changes: add/update/remove items, update rarity/weights, recalc droprates, set Draft
        await ValidateProductStockForBlindBoxAsync(blindBox, items);

        var dropRatesDraft = CalculateDropRates(items);
        var nowDraft = _time.GetCurrentTime();

        // Update existing items or soft-remove if not in request
        var dtoByProduct = items.ToDictionary(i => i.ProductId, i => i);
        var processedExistingProductIds = new HashSet<Guid>();

        foreach (var existing in existingItems)
        {
            if (dtoByProduct.TryGetValue(existing.ProductId, out var dto))
            {
                existing.ProductId = dto.ProductId;
                existing.Quantity = dto.Quantity;
                existing.DropRate = dropRatesDraft[dto];
                existing.IsSecret = dto.Rarity == RarityName.Secret;
                existing.IsActive = true;
                existing.UpdatedAt = nowDraft;
                existing.UpdatedBy = currentUserId;
                await _unitOfWork.BlindBoxItems.Update(existing);

                var rarity = await _unitOfWork.RarityConfigs.FirstOrDefaultAsync(r =>
                    r.BlindBoxItemId == existing.Id && !r.IsDeleted);

                if (rarity != null)
                {
                    rarity.Name = dto.Rarity;
                    rarity.Weight = dto.Weight;
                    rarity.IsSecret = dto.Rarity == RarityName.Secret;
                    rarity.UpdatedAt = nowDraft;
                    rarity.UpdatedBy = currentUserId;
                    await _unitOfWork.RarityConfigs.Update(rarity);
                }

                processedExistingProductIds.Add(existing.ProductId);
            }
            else
            {
                await _unitOfWork.BlindBoxItems.SoftRemove(existing);
            }
        }

        // Add new items
        var newItems = new List<BlindBoxItem>();
        var newRarities = new List<RarityConfig>();
        foreach (var dto in items)
        {
            if (processedExistingProductIds.Contains(dto.ProductId)) continue;

            var product = products.FirstOrDefault(p => p.Id == dto.ProductId);
            if (product == null)
                throw ErrorHelper.BadRequest($"Sản phẩm {dto.ProductId} không tồn tại hoặc không thuộc bạn.");
            if (dto.Quantity > product.TotalStockQuantity)
                throw ErrorHelper.BadRequest($"Sản phẩm '{product.Name}' không đủ số lượng trong kho.");

            var newItem = new BlindBoxItem
            {
                Id = Guid.NewGuid(),
                BlindBoxId = blindBoxId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                DropRate = dropRatesDraft[dto],
                IsSecret = dto.Rarity == RarityName.Secret,
                IsActive = true,
                CreatedAt = nowDraft,
                CreatedBy = currentUserId
            };
            newItems.Add(newItem);

            newRarities.Add(new RarityConfig
            {
                Id = Guid.NewGuid(),
                BlindBoxItemId = newItem.Id,
                Name = dto.Rarity,
                Weight = dto.Weight,
                IsSecret = dto.Rarity == RarityName.Secret,
                CreatedAt = nowDraft,
                CreatedBy = currentUserId
            });
        }

        if (newItems.Any())
            await _unitOfWork.BlindBoxItems.AddRangeAsync(newItems);
        if (newRarities.Any())
            await _unitOfWork.RarityConfigs.AddRangeAsync(newRarities);

        blindBox.HasSecretItem = items.Any(i => i.Rarity == RarityName.Secret);
        blindBox.SecretProbability = dropRatesDraft
            .Where(kv => kv.Key.Rarity == RarityName.Secret)
            .Sum(kv => kv.Value);

        blindBox.TotalQuantity = items.Sum(i => i.Quantity);
        blindBox.Status = BlindBoxStatus.Draft;
        blindBox.UpdatedAt = nowDraft;
        blindBox.UpdatedBy = currentUserId;

        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();

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
                throw ErrorHelper.BadRequest(
                    "Một hoặc nhiều sản phẩm trong Blind Box không còn tồn tại hoặc đã bị xóa.");

            // Kiểm tra AvailableToSell thay vì Stock
            if (item.Quantity > product.AvailableToSell)
                throw ErrorHelper.BadRequest(
                    $"Sản phẩm '{product.Name}' không đủ số lượng khả dụng để đưa vào Blind Box.");

            // Reserve stock thay vì trừ TotalStockQuantity
            product.ReservedInBlindBox += item.Quantity;

            // Cập nhật status dựa trên AvailableToSell
            if (product.AvailableToSell == 0 && product.Status != ProductStatus.InActive)
                product.Status = ProductStatus.OutOfStock;
        }

        await _unitOfWork.Products.UpdateRange(products);
        await _unitOfWork.BlindBoxes.Update(blindBox);
        await _unitOfWork.SaveChangesAsync();

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
                EffectiveTo = now.AddYears(1),
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

            // --- Release reserved stock cho từng item ---
            var productIds = blindBox.BlindBoxItems.Select(i => i.ProductId).Distinct().ToList();
            var products = await _unitOfWork.Products.GetAllAsync(p =>
                productIds.Contains(p.Id) && !p.IsDeleted && p.SellerId == blindBox.Seller.Id);

            foreach (var item in blindBox.BlindBoxItems)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                {
                    // Release reserved stock thay vì cộng vào TotalStockQuantity
                    product.ReservedInBlindBox -= item.Quantity;

                    // Cập nhật status nếu sản phẩm từ OutOfStock trở thành Available
                    if (product.AvailableToSell > 0 && product.Status == ProductStatus.OutOfStock)
                        product.Status = ProductStatus.Active;
                }
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
            new NotificationDto
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

        if (blindBox.BlindBoxItems == null || !blindBox.BlindBoxItems.Any())
            return await GetBlindBoxByIdAsync(blindBoxId);

        var items = blindBox.BlindBoxItems.ToList();

        // Release reserved stock
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.GetAllAsync(p =>
            productIds.Contains(p.Id) && !p.IsDeleted && p.SellerId == seller.Id);

        foreach (var item in items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
            {
                // Release reserved stock thay vì cộng vào TotalStockQuantity
                product.ReservedInBlindBox -= item.Quantity;

                // Cập nhật status nếu sản phẩm từ OutOfStock trở thành Available
                if (product.AvailableToSell > 0 && product.Status == ProductStatus.OutOfStock)
                    product.Status = ProductStatus.Active;
            }
        }

        await _unitOfWork.Products.UpdateRange(products);
        await _unitOfWork.BlindBoxItems.SoftRemoveRange(items);
        await _unitOfWork.SaveChangesAsync();

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
                product.TotalStockQuantity += item.Quantity;
        }

        await _unitOfWork.Products.UpdateRange(products);
        await _unitOfWork.SaveChangesAsync();


        var result = _mapperService.Map<BlindBox, BlindBoxDetailDto>(blindBox);
        result.Items = blindBox.BlindBoxItems
            .OrderByDescending(i => i.DropRate)
            .Select(item => new BlindBoxItemResponseDto
            {
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? string.Empty,
                DropRate = item.DropRate,
                ImageUrl = item.Product?.ImageUrls.FirstOrDefault(),
                Quantity = item.Quantity
            }).ToList();


        return result;
    }

    public Dictionary<BlindBoxItemRequestDto, decimal> CalculateDropRates(List<BlindBoxItemRequestDto> items)
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

    #region private methods

    private async Task ValidateProductStockForBlindBoxAsync(
        BlindBox blindBox,
        List<BlindBoxItemRequestDto> items)
    {
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products
            .GetAllAsync(p => productIds.Contains(p.Id) && !p.IsDeleted);

        foreach (var item in items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null)
                throw ErrorHelper.BadRequest(
                    $"Không tìm thấy sản phẩm với ID: {item.ProductId}. Vui lòng kiểm tra lại.");

            // Tổng số lượng cần thiết cho product này
            var requiredQuantity = item.Quantity * blindBox.TotalQuantity;

            if (requiredQuantity > product.TotalStockQuantity)
                throw ErrorHelper.BadRequest(
                    $"Sản phẩm '{product.Name}' không đủ số lượng hiện có. " +
                    $"Do BlindBox có {blindBox.TotalQuantity} hộp, mỗi hộp cần {item.Quantity} sản phẩm, " +
                    $"nên tổng cộng cần {requiredQuantity}, nhưng chỉ có {product.TotalStockQuantity}."
                );
        }
    }

    private void ValidateBlindBoxItemsFullRule(List<BlindBoxItemRequestDto> items)
    {
        if (items.Count != 6 && items.Count != 12)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Số lượng item không hợp lệ [ActualCount={items.Count}]. Blind Box phải có đúng 6 hoặc 12 sản phẩm.");
            throw ErrorHelper.BadRequest("Mỗi Blind Box phải chứa đúng 6 hoặc 12 sản phẩm.");
        }

        // ✅ Validate ProductId không trùng nhau
        var duplicateProductIds = items.GroupBy(i => i.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateProductIds.Any())
        {
            var duplicateList = string.Join(", ", duplicateProductIds);
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Phát hiện ProductId trùng nhau [DuplicateProductIds={duplicateList}].");
            throw ErrorHelper.BadRequest(
                $"Mỗi sản phẩm chỉ được thêm một lần vào Blind Box. Các sản phẩm sau đã bị trùng: {duplicateList}.");
        }

        var countSecret = items.Count(i => i.Rarity == RarityName.Secret);
        if (countSecret < 1)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Không có item Secret [SecretCount={countSecret}]. Blind Box phải có ít nhất 1 item Secret.");
            throw ErrorHelper.BadRequest("Mỗi Blind Box phải có ít nhất một vật phẩm được đánh dấu là 'Secret'.");
        }

        if (countSecret > 1)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Có nhiều hơn 1 item Secret [SecretCount={countSecret}]. Mỗi BlindBox chỉ được phép có nhiều nhất 1 item Secret.");
            throw ErrorHelper.BadRequest("Mỗi Blind Box chỉ được phép có tối đa một vật phẩm 'Secret'.");
        }

        var validRarities = Enum.GetValues(typeof(RarityName)).Cast<RarityName>().ToList();
        var invalids = items.Where(i => !validRarities.Contains(i.Rarity)).ToList();
        if (invalids.Any())
        {
            var invalidList = string.Join(", ", invalids.Select(i => $"{i.Rarity}"));
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Phát hiện rarity không hợp lệ [InvalidRarity={invalidList}]. Chỉ chấp nhận các rarity: Common, Rare, Epic, Secret.");
            throw ErrorHelper.BadRequest(
                "Độ hiếm của vật phẩm không hợp lệ. Vui lòng chỉ sử dụng các giá trị: Common, Rare, Epic, Secret.");
        }

        var totalWeight = items.Sum(i => i.Weight);
        if (totalWeight != 100)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Tổng trọng số không hợp lệ [TotalWeight={totalWeight}]. Tổng trọng số (Weight) phải đúng bằng 100.");
            throw ErrorHelper.BadRequest("Tổng trọng số (Weight) của tất cả vật phẩm phải chính xác là 100.");
        }

        // Validate thứ tự weight: Common ≥ Rare ≥ Epic ≥ Secret
        var tierOrder = new List<RarityName> { RarityName.Common, RarityName.Rare, RarityName.Epic, RarityName.Secret };
        var groupWeights = tierOrder
            .Select(tier => items.Where(i => i.Rarity == tier).Sum(i => i.Weight))
            .ToList();

        for (var i = 1; i < groupWeights.Count; i++)
            if (groupWeights[i] > 0 && groupWeights[i - 1] > 0 && groupWeights[i] > groupWeights[i - 1])
            {
                var detail = string.Join(", ", tierOrder.Select((r, idx) => $"{r}={groupWeights[idx]}"));
                _logger.Warn(
                    $"[ValidateBlindBoxItemsFullRule] Tổng trọng số tier sau lớn hơn tier trước [TierOrder={detail}]. Không cho phép trọng số của tier sau lớn hơn tier trước.");
                throw ErrorHelper.BadRequest(
                    "Trọng số của các bậc độ hiếm phải tuân theo quy tắc: Common ≥ Rare ≥ Epic ≥ Secret.");
            }

        var secretWeight = items.Where(i => i.Rarity == RarityName.Secret).Sum(i => i.Weight);
        if (secretWeight > 10)
        {
            _logger.Warn(
                $"[ValidateBlindBoxItemsFullRule] Secret weight vượt quá giới hạn [SecretWeight={secretWeight}]. Tối đa cho phép = 10.");
            throw ErrorHelper.BadRequest("Vật phẩm Secret không được vượt quá 10% tổng trọng số.");
        }
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

    private Task<BlindBoxDetailDto> MapBlindBoxToDtoAsync(BlindBox blindBox)
    {
        var dto = _mapperService.Map<BlindBox, BlindBoxDetailDto>(blindBox);

        dto.BlindBoxStockStatus = blindBox.TotalQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock;

        if (blindBox.TotalQuantity <= 0 && blindBox.Status == BlindBoxStatus.Approved)
            _logger.Warn($"[MapBlindBoxToDtoAsync] BlindBox {blindBox.Id} đã hết hàng nhưng status vẫn là Approved");

        dto.Brand = blindBox.Seller?.CompanyName;
        dto.Items = MapToBlindBoxItemDtos(blindBox.BlindBoxItems);

        // ✅ Tính tổng weight theo từng rarity từ dto.Items
        dto.TierWeights = dto.Items
            .GroupBy(i => i.Rarity)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Weight)
            );

        return Task.FromResult(dto);
    }

    private async Task<IQueryable<BlindBox>> ApplyFilters(IQueryable<BlindBox> query, BlindBoxQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(userId);

        // Kiểm tra nếu user là Seller và áp dụng filter theo SellerId
        if (user != null && user.RoleName == RoleType.Seller) query = query.Where(b => b.Seller!.UserId == userId);

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
            var categoryIds = _categoryService.GetAllChildCategoryIdsAsync(param.CategoryId.Value).GetAwaiter()
                .GetResult();
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

        return query;
    }

    private IQueryable<BlindBox> ApplySorting(IQueryable<BlindBox> query, BlindBoxQueryParameter param)
    {
        if (param.Desc)
            query = query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt);
        else
            query = query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);
        return query;
    }

    private List<BlindBoxItemResponseDto> MapToBlindBoxItemDtos(IEnumerable<BlindBoxItem> items)
    {
        return items
            .OrderByDescending(item => item.DropRate) // Sắp xếp giảm dần theo DropRate
            .Select(item => new BlindBoxItemResponseDto
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