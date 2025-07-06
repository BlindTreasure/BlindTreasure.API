using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class UnboxingService : IUnboxingService
{
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;

    public UnboxingService(ILoggerService loggerService, IUnitOfWork unitOfWork, IClaimsService claimsService,
        ICurrentTime currentTime)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
    }
    public async Task<UnboxResultDto> UnboxAsync(Guid customerBlindBoxId)
    {
        var userId = _claimsService.CurrentUserId;
        var now = _currentTime.GetCurrentTime();

        // 1. Kiểm tra quyền và trạng thái hộp
        var customerBox = await GetValidCustomerBlindBoxAsync(customerBlindBoxId, userId);
        var blindBox = customerBox.BlindBox;

        // 2. Lấy danh sách item hợp lệ (bao gồm xác suất và rarity config)
        var items = blindBox.BlindBoxItems
            .Where(i => !i.IsDeleted && i.IsActive && i.Quantity > 0)
            .ToList();

        if (!items.Any())
            throw ErrorHelper.BadRequest("Hộp này không còn item nào để mở.");

        // 3. Lấy xác suất đã duyệt (ProbabilityConfig) hiện tại của từng item
        var probabilities = new Dictionary<BlindBoxItem, decimal>();
        foreach (var item in items)
        {
            var pConfig = item.ProbabilityConfigs
                .Where(p => p.EffectiveFrom <= now && p.EffectiveTo >= now)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            probabilities[item] = pConfig?.Probability ?? 0;
        }

        // 4. Random item dựa trên xác suất
        var selectedItem = WeightedRandom(probabilities);
        if (selectedItem == null)
            throw ErrorHelper.Internal("Không thể chọn được item từ hộp.");

        // 5. Cập nhật DB (trừ số lượng, cộng Inventory cho user, đánh dấu hộp đã mở)
        await GrantUnboxedItemToUser(selectedItem, customerBox, userId, now);

        return new UnboxResultDto
        {
            ProductId = selectedItem.ProductId,
            Rarity = selectedItem.RarityConfig?.Name,
            Weight = selectedItem.RarityConfig?.Weight ?? 0,
            UnboxedAt = now
        };
    }

    public async Task<List<ProbabilityConfig>> GetApprovedProbabilitiesAsync(Guid blindBoxId)
    {
        var items = await _unitOfWork.BlindBoxItems.GetQueryable()
            .Where(i => i.BlindBoxId == blindBoxId && !i.IsDeleted)
            .Include(i => i.ProbabilityConfigs)
            .ToListAsync();

        return items
            .SelectMany(i => i.ProbabilityConfigs
                .Where(p => p.EffectiveFrom <= DateTime.UtcNow && p.EffectiveTo >= DateTime.UtcNow))
            .ToList();
    }

    #region Private methods

    private async Task<CustomerBlindBox> GetValidCustomerBlindBoxAsync(Guid id, Guid userId)
    {
        var box = await _unitOfWork.CustomerBlindBoxes.GetQueryable()
            .Include(cb => cb.BlindBox)
            .ThenInclude(bb => bb.BlindBoxItems)
            .ThenInclude(bbi => bbi.ProbabilityConfigs)
            .FirstOrDefaultAsync(cb => cb.Id == id);

        if (box == null)
        {
            _loggerService.Warn($"[Unbox] Hộp không tồn tại. BoxId={id}, UserId={userId}");
            throw ErrorHelper.BadRequest("Không tìm thấy hộp hợp lệ để mở.");
        }

        if (box.UserId != userId)
        {
            _loggerService.Warn(
                $"[Unbox] Hộp không thuộc về người dùng. BoxId={id}, OwnerId={box.UserId}, RequesterId={userId}");
            throw ErrorHelper.BadRequest("Không có quyền mở hộp này.");
        }

        if (box.IsDeleted)
        {
            _loggerService.Warn($"[Unbox] Hộp đã bị xóa. BoxId={id}, UserId={userId}");
            throw ErrorHelper.BadRequest("Hộp không hợp lệ (đã bị xóa).");
        }

        if (box.IsOpened)
        {
            _loggerService.Warn(
                $"[Unbox] Hộp đã được mở trước đó. BoxId={id}, UserId={userId}, OpenedAt={box.OpenedAt}");
            throw ErrorHelper.BadRequest("Hộp đã được mở trước đó.");
        }

        return box;
    }

    private async Task<InventoryItem> GrantUnboxedItemToUser(
        BlindBoxItem selectedItem,
        CustomerBlindBox customerBox,
        Guid userId,
        DateTime now)
    {
        selectedItem.Quantity--;

        if (selectedItem.Quantity == 0)
            await NotifyOutOfStockAsync(customerBox.BlindBox, selectedItem);

        var inventory = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = selectedItem.ProductId,
            UserId = userId,
            Quantity = 1,
            Location = "HCM",
            Status = "Available",
            CreatedAt = now,
            CreatedBy = userId
        };

        customerBox.IsOpened = true;
        customerBox.OpenedAt = now;

        await _unitOfWork.InventoryItems.AddAsync(inventory);
        await _unitOfWork.CustomerBlindBoxes.Update(customerBox);
        await _unitOfWork.BlindBoxItems.Update(selectedItem);
        await _unitOfWork.SaveChangesAsync();

        return inventory;
    }


    private static T? WeightedRandom<T>(Dictionary<T, decimal> weightedDict)
    {
        var totalWeight = weightedDict.Values.Sum();
        if (totalWeight <= 0) return default;

        var rand = new Random();
        var roll = (decimal)rand.NextDouble() * totalWeight;
        decimal cumulative = 0;

        foreach (var kvp in weightedDict)
        {
            cumulative += kvp.Value;
            if (roll <= cumulative)
                return kvp.Key;
        }

        return default;
    }

    private async Task NotifyOutOfStockAsync(BlindBox blindBox, BlindBoxItem item)
    {
        blindBox.Status = BlindBoxStatus.Rejected; // hoặc enum riêng như Disabled/OutOfStock nếu có
        await _unitOfWork.BlindBoxes.Update(blindBox);

        var sellerUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == blindBox.Seller.UserId);
        if (sellerUser != null)
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = sellerUser.Id,
                Title = $"Item hết hàng trong {blindBox.Name}",
                Message = $"Sản phẩm '{item.Product.Name}' trong blind box đã hết số lượng.",
                Type = NotificationType.System,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _claimsService.CurrentUserId
            });
        // TODO: Gửi email qua EmailService nếu có
    }

    #endregion
}