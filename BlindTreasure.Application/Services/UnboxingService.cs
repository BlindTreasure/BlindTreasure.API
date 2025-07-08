using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
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
    private readonly INotificationService _notificationService;

    public UnboxingService(ILoggerService loggerService, IUnitOfWork unitOfWork, IClaimsService claimsService,
        ICurrentTime currentTime, INotificationService notificationService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _notificationService = notificationService;
    }

    public async Task<UnboxResultDto> UnboxAsync(Guid customerBlindBoxId)
    {
        var userId = _claimsService.CurrentUserId;
        var now = _currentTime.GetCurrentTime();

        // 1. Kiểm tra quyền và trạng thái hộp
        var customerBox = await GetValidCustomerBlindBoxAsync(customerBlindBoxId, userId);
        var blindBox = customerBox.BlindBox;

        // 2. Lấy danh sách item hợp lệ
        var items = blindBox?.BlindBoxItems
            .Where(i => !i.IsDeleted && i.IsActive && i.Quantity > 0)
            .ToList();

        if (items != null && !items.Any())
            throw ErrorHelper.BadRequest("Hộp này không còn item nào để mở.");

        // 3 & 4. Random item theo xác suất (dùng hàm mới)
        var selectedItem = GetRandomItemByProbability(items, now);
        if (selectedItem == null)
            throw ErrorHelper.Internal("Không thể chọn được item từ hộp.");

        // 5. Cập nhật DB (trừ số lượng, cộng Inventory cho user, đánh dấu hộp đã mở)
        await GrantUnboxedItemToUser(selectedItem, customerBox, userId, now);

        return new UnboxResultDto
        {
            ProductId = selectedItem.ProductId,
            Rarity = selectedItem.RarityConfig?.Name,
            DropRate = selectedItem.DropRate,
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
        var customerBox = await _unitOfWork.CustomerBlindBoxes.GetQueryable()
            .Include(cb => cb.BlindBox)
            .ThenInclude(bb => bb.BlindBoxItems)
            .ThenInclude(bbi => bbi.RarityConfig)
            .Include(cb => cb.BlindBox)
            .ThenInclude(bb => bb.BlindBoxItems)
            .ThenInclude(bbi => bbi.ProbabilityConfigs)
            .Include(cb => cb.BlindBox)
            .ThenInclude(bb => bb.BlindBoxItems)
            .ThenInclude(bbi => bbi.Product)
            .FirstOrDefaultAsync(cb => cb.Id == id);

        if (customerBox == null)
        {
            _loggerService.Warn($"[Unbox] Hộp không tồn tại. BoxId={id}, UserId={userId}");
            throw ErrorHelper.BadRequest("Không tìm thấy hộp hợp lệ để mở.");
        }

        if (customerBox.UserId != userId)
        {
            _loggerService.Warn(
                $"[Unbox] Hộp không thuộc về người dùng. BoxId={id}, OwnerId={customerBox.UserId}, RequesterId={userId}");
            throw ErrorHelper.BadRequest("Không có quyền mở hộp này.");
        }

        if (customerBox.IsDeleted)
        {
            _loggerService.Warn($"[Unbox] Hộp đã bị xóa. BoxId={id}, UserId={userId}");
            throw ErrorHelper.BadRequest("Hộp không hợp lệ (đã bị xóa).");
        }

        if (customerBox.IsOpened)
        {
            _loggerService.Warn(
                $"[Unbox] Hộp đã được mở trước đó. BoxId={id}, UserId={userId}, OpenedAt={customerBox.OpenedAt}");
            throw ErrorHelper.BadRequest("Hộp đã được mở trước đó.");
        }

        return customerBox;
    }

    private async Task GrantUnboxedItemToUser(
        BlindBoxItem selectedItem,
        CustomerBlindBox customerBox,
        Guid userId,
        DateTime now)
    {
        selectedItem.Quantity--;

        if (selectedItem.Quantity == 0)
            await NotifyOutOfStockAsync(customerBox.BlindBox, selectedItem);

        var defaultAddress = await _unitOfWork.Addresses.GetQueryable()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault && !a.IsDeleted);

        var inventory = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = selectedItem.ProductId,
            UserId = userId,
            Quantity = 1,
            Location = defaultAddress?.Province ?? "HCM", // giữ "HCM" nếu không có
            Status = InventoryItemStatus.Available,
            AddressId = defaultAddress?.Id,
            IsFromBlindBox = true,
            SourceCustomerBlindBoxId = customerBox.Id,
            CreatedAt = now,
            CreatedBy = userId
        };

        customerBox.IsOpened = true;
        customerBox.OpenedAt = now;

        await _unitOfWork.InventoryItems.AddAsync(inventory);
        await _unitOfWork.CustomerBlindBoxes.Update(customerBox);
        await _unitOfWork.BlindBoxItems.Update(selectedItem);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task NotifyOutOfStockAsync(BlindBox blindBox, BlindBoxItem item)
    {
        blindBox.Status = BlindBoxStatus.Rejected; // hoặc enum riêng như Disabled/OutOfStock nếu có
        await _unitOfWork.BlindBoxes.Update(blindBox);

        var sellerUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == blindBox.Seller.UserId);
        if (sellerUser != null)
            await _notificationService.PushNotificationToUser(
                sellerUser.Id,
                new NotificationDTO {
                    Title = $"Item hết hàng trong {blindBox.Name}",
                    Message = $"Sản phẩm '{item.Product.Name}' trong blind box đã hết số lượng.",
                    Type = NotificationType.System
                }
            );
        // TODO: Gửi email qua EmailService nếu có
    }

    private BlindBoxItem? GetRandomItemByProbability(List<BlindBoxItem> items, DateTime now)
    {
        var probabilities = new Dictionary<BlindBoxItem, decimal>();
        foreach (var item in items)
        {
            var pConfig = item.ProbabilityConfigs
                .Where(p => p.EffectiveFrom <= now && p.EffectiveTo >= now)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            probabilities[item] = pConfig?.Probability ?? 0;
        }

        var totalProbability = probabilities.Values.Sum();

        // Header bảng
        var separator =
            "+----+--------------------------------------+--------------------------+--------+----------+";
        var header =
            "| No | ProductId                            | ProductName              | Rarity | Weight   | Prob(%) |";
        _loggerService.Info("[Gacha] =========== RANDOM BLINDBOX ITEM ===========");
        _loggerService.Info(separator);
        _loggerService.Info(header);
        _loggerService.Info(separator);

        var idx = 1;
        foreach (var kvp in probabilities)
        {
            var item = kvp.Key;
            var prob = kvp.Value;
            var productName = (item.Product?.Name ?? "").PadRight(24).Substring(0, 24);
            var rarity = item.RarityConfig?.Name.ToString() ?? "Unknown";
            var weight = (item.RarityConfig?.Weight ?? 0).ToString().PadLeft(6);
            var probText = prob.ToString("0.##").PadLeft(7);

            _loggerService.Info(
                $"| {idx.ToString().PadLeft(2)} | {item.ProductId} | {productName} | {rarity.PadRight(8)} | {weight} | {probText} |");
            idx++;
        }

        _loggerService.Info(separator);
        _loggerService.Info($"[Gacha] Tổng xác suất: {totalProbability}%");

        if (totalProbability <= 0)
        {
            _loggerService.Warn("[Gacha] Tổng xác suất bằng 0, không thể random.");
            return null;
        }

        var rand = new Random();
        var roll = (decimal)rand.NextDouble() * totalProbability;
        _loggerService.Info($"[Gacha] Số random sinh ra: {roll:0.#####} (range 0 ~ {totalProbability})");

        decimal cumulative = 0;
        BlindBoxItem? selectedItem = null;
        var selectIndex = 1;
        foreach (var kvp in probabilities)
        {
            cumulative += kvp.Value;
            _loggerService.Info($"[Gacha] [Cộng dồn] #{selectIndex}: {cumulative:0.###}");
            if (roll <= cumulative)
            {
                selectedItem = kvp.Key;
                var item = selectedItem;
                _loggerService.Info("[Gacha] >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
                _loggerService.Info(
                    $"[Gacha] KẾT QUẢ: Sản phẩm [{item.ProductId}] | {item.Product?.Name} | Rarity={item.RarityConfig?.Name} | Weight={item.RarityConfig?.Weight}");
                _loggerService.Info("[Gacha] <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                break;
            }

            selectIndex++;
        }

        if (selectedItem == null)
            _loggerService.Warn("[Gacha] Không chọn được item nào!");

        _loggerService.Info("[Gacha] =========== KẾT THÚC RANDOM BLINDBOX ITEM ===========");

        return selectedItem;
    }

    #endregion
}