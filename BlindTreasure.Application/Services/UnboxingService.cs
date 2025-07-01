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
        var now = DateTime.UtcNow;

        // PHASE 1: Kiểm tra hộp có hợp lệ để mở không
        var customerBox = await GetValidCustomerBlindBoxAsync(customerBlindBoxId, userId);
        var blindBox = customerBox.BlindBox;

        // PHASE 2: Chọn ngẫu nhiên 1 item từ hộp theo tỷ lệ đã được duyệt
        var selectedItem = await SelectItemToUnbox(blindBox);
        if (selectedItem == null)
        {
            _loggerService.Warn(
                $"[Unbox] Không thể chọn item từ hộp {customerBlindBoxId} (User {userId}) - Không có item hợp lệ.");
            throw ErrorHelper.Internal("Không thể chọn được item từ hộp.");
        }

        // PHASE 3: Gán item cho người dùng
        await GrantUnboxedItemToUser(selectedItem, customerBox, userId, now);

        // PHASE 4: Ghi log kết quả
        _loggerService.Info(
            $"[Unbox] User {userId} mở hộp {customerBlindBoxId} nhận được item {selectedItem.ProductId}");

        return new UnboxResultDto
        {
            ProductId = selectedItem.ProductId,
            Rarity = selectedItem.Rarity,
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

    private async Task<BlindBoxItem?> SelectItemToUnbox(BlindBox blindBox)
    {
        var items = blindBox.BlindBoxItems
            .Where(i => !i.IsDeleted && i.IsActive && i.Quantity > 0)
            .ToList();

        if (!items.Any())
        {
            _loggerService.Warn($"[Unbox] Hộp {blindBox.Id} không còn item hợp lệ để mở.");
            throw ErrorHelper.BadRequest("Hộp này không còn item nào để mở.");
        }

        var probabilities = await GetApprovedProbabilitiesAsync(blindBox.Id);
        var selected = RandomByRarityAndProbability(items, probabilities);

        if (selected == null)
            _loggerService.Warn(
                $"[Unbox] Không chọn được item từ BlindBox {blindBox.Id} sau khi random theo xác suất.");

        return selected;
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

    private BlindBoxItem? RandomByRarityAndProbability(List<BlindBoxItem> items, List<ProbabilityConfig> probabilities)
    {
        // Bước 1: Nhóm theo rarity
        var rarityGroups = items
            .GroupBy(i => i.Rarity)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Tính tổng drop rate cho mỗi rarity
        var rarityDropRates = new Dictionary<BlindBoxRarity, decimal>();

        foreach (var rarity in rarityGroups.Keys)
        {
            var total = rarityGroups[rarity]
                .Select(i => probabilities.FirstOrDefault(p => p.BlindBoxItemId == i.Id)?.Probability ?? 0)
                .Sum();

            rarityDropRates[rarity] = total;
        }

        // Bước 2: Random rarity
        var selectedRarity = WeightedRandom(rarityDropRates);
        if (!rarityGroups.ContainsKey(selectedRarity))
            return null;

        // Bước 3: Random item trong nhóm rarity đó
        var itemGroup = rarityGroups[selectedRarity];

        var itemDropRates = itemGroup.ToDictionary(
            i => i,
            i => probabilities.FirstOrDefault(p => p.BlindBoxItemId == i.Id)?.Probability ?? 0);

        return WeightedRandom(itemDropRates);
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