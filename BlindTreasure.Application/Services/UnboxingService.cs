using System.Text;
using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.DTOs.UnboxLogDTOs;
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
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

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
        var (selectedItem, roll, probabilityMap) = GetRandomItemByProbability(items, now);
        if (selectedItem == null)
            throw ErrorHelper.Internal("Không thể chọn được item từ hộp.");

        // Ghi log vào bảng
        await _unitOfWork.BlindBoxUnboxLogs.AddAsync(new BlindBoxUnboxLog
        {
            Id = Guid.NewGuid(),
            CustomerBlindBoxId = customerBox.Id,
            UserId = userId,
            ProductId = selectedItem.ProductId,
            ProductName = selectedItem.Product?.Name ?? "",
            Rarity = selectedItem.RarityConfig?.Name ?? RarityName.Common,
            DropRate = selectedItem.DropRate,
            RollValue = roll,
            ProbabilityTableJson = JsonSerializer.Serialize(probabilityMap.Select(p => new
            {
                p.Key.ProductId,
                ProductName = p.Key.Product?.Name,
                Rarity = p.Key.RarityConfig?.Name,
                DropRate = p.Value
            })),
            UnboxedAt = now,
            BlindBoxName = blindBox?.Name ?? "",
            Reason = BuildUnboxReasonForFrontend(probabilityMap, roll, selectedItem)
        });

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

    public async Task<List<UnboxLogDto>> GetLogsAsync(Guid? userId, Guid? productId)
    {
        var query = _unitOfWork.BlindBoxUnboxLogs.GetQueryable();

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (productId.HasValue)
            query = query.Where(x => x.ProductId == productId.Value);

        var result = await query
            .OrderByDescending(x => x.UnboxedAt)
            .Take(100)
            .Select(x => new UnboxLogDto
            {
                Id = x.Id,
                CustomerBlindBoxId = x.CustomerBlindBoxId,
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                Rarity = x.Rarity,
                DropRate = x.DropRate,
                RollValue = x.RollValue,
                UnboxedAt = x.UnboxedAt,
                BlindBoxName = x.BlindBoxName,
                Reason = x.Reason
            })
            .ToListAsync();

        return result;
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

    private string BuildUnboxReasonForFrontend(
        Dictionary<BlindBoxItem, decimal> probabilities,
        decimal roll,
        BlindBoxItem selectedItem)
    {
        var sb = new StringBuilder();
        var total = probabilities.Values.Sum();

        // Tiêu đề
        sb.AppendLine($"### Gacha Roll");
        sb.AppendLine($"- **Roll:** {Math.Round(roll, 4):N4}");
        sb.AppendLine($"- **Tổng xác suất:** {Math.Round(total, 2):N2}%\n");

        sb.AppendLine("### Danh sách item:");

        decimal cumulative = 0;
        foreach (var kvp in probabilities
                     .OrderByDescending(p => p.Value)
                     .ThenBy(p => p.Key.ProductId))
        {
            var start = cumulative;
            var end = start + kvp.Value;
            cumulative = end;

            var name = kvp.Key.Product?.Name ?? "Không rõ";
            var rarity = kvp.Key.RarityConfig?.Name.ToString() ?? "Không rõ";
            var drop = Math.Round(kvp.Value, 2);
            var range = $"{Math.Round(start, 2):N2}% – {Math.Round(end, 2):N2}%";
            var selectedMark = kvp.Key.Id == selectedItem.Id ? " **<= ĐÃ TRÚNG**" : "";

            sb.AppendLine(
                $"- **{name}** (Độ hiếm: *{rarity}*, Tỉ lệ: {drop:N2}%, Khoảng: {range}){selectedMark}"
            );
        }

        sb.AppendLine(
            $"\n**Kết quả:** `{selectedItem.Product?.Name}` (DropRate = {Math.Round(selectedItem.DropRate, 2):N2}%)");

        return sb.ToString();
    }


    private async Task<CustomerBlindBox> GetValidCustomerBlindBoxAsync(Guid id, Guid userId)
    {
        var customerBox = await _unitOfWork.CustomerBlindBoxes.GetQueryable()
            .Include(cb => cb.BlindBox)
            .ThenInclude(bb => bb.BlindBoxItems)
            .ThenInclude(bbi => bbi.ProbabilityConfigs)
            .Include(cb => cb.BlindBox)
            .ThenInclude(bb => bb.BlindBoxItems)
            .ThenInclude(bbi => bbi.Product)
            .FirstOrDefaultAsync(cb => cb.Id == id);

        if (customerBox == null || customerBox.UserId != userId || customerBox.IsDeleted || customerBox.IsOpened)
        {
            var msg = customerBox == null
                ? "Không tìm thấy hộp hợp lệ để mở."
                : customerBox.IsDeleted
                    ? "Hộp không hợp lệ (đã bị xóa)."
                    : customerBox.IsOpened
                        ? "Hộp đã được mở trước đó."
                        : "Không có quyền mở hộp này.";
            throw ErrorHelper.BadRequest(msg);
        }

        // Load thêm RarityConfig cho từng BlindBoxItem (manual)
        var itemIds = customerBox.BlindBox.BlindBoxItems.Select(i => i.Id).ToList();
        var rarities = await _unitOfWork.RarityConfigs.GetQueryable()
            .Where(r => itemIds.Contains(r.BlindBoxItemId))
            .ToListAsync();

        foreach (var item in customerBox.BlindBox.BlindBoxItems)
            item.RarityConfig = rarities.FirstOrDefault(r => r.BlindBoxItemId == item.Id);

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
                new NotificationDTO
                {
                    Title = $"Item hết hàng trong {blindBox.Name}",
                    Message = $"Sản phẩm '{item.Product.Name}' trong blind box đã hết số lượng.",
                    Type = NotificationType.System
                }
            );
        // TODO: Gửi email qua EmailService nếu có
    }

    private (BlindBoxItem? Item, decimal Roll, Dictionary<BlindBoxItem, decimal> Probabilities)
        GetRandomItemByProbability(List<BlindBoxItem> items, DateTime now)
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

        if (totalProbability <= 0)
        {
            _loggerService.Warn("[Gacha] Tổng xác suất bằng 0, không thể random.");
            return (null, 0, probabilities);
        }

        var sorted = probabilities
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key.ProductId)
            .ToList();

        // ✅ Log bảng sắp xếp
        var orderLog = new StringBuilder();
        orderLog.AppendLine("[Gacha] Thứ tự item sau khi sắp xếp để random:");
        decimal start = 0;
        foreach (var kvp in sorted)
        {
            var end = start + kvp.Value;
            var productName = kvp.Key.Product?.Name ?? "Unknown";
            var rarity = kvp.Key.RarityConfig?.Name.ToString() ?? "Unknown";
            orderLog.AppendLine(
                $"- {productName} (Rarity: {rarity}, DropRate: {kvp.Value:N2}%) → Range: [{start:N2} – {end:N2}]"
            );
            start = end;
        }

        _loggerService.Info(orderLog.ToString());

        // ✅ Sinh roll và chọn item theo khoảng
        var rand = new Random();
        var roll = (decimal)rand.NextDouble() * totalProbability;
        decimal cumulative = 0;
        BlindBoxItem? selectedItem = null;

        foreach (var kvp in sorted)
        {
            cumulative += kvp.Value;
            if (roll <= cumulative)
            {
                selectedItem = kvp.Key;
                break;
            }
        }

        return (selectedItem, roll, probabilities);
    }

    #endregion
}