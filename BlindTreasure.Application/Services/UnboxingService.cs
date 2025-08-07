using System.Text;
using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.DTOs.UnboxLogDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
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

    public async Task<Pagination<UnboxLogDto>> GetLogsAsync(PaginationParameter param, Guid? userId, Guid? productId)
    {
        var query = _unitOfWork.BlindBoxUnboxLogs.GetQueryable()
            .Include(x => x.User) // Include User để lấy FullName
            .Where(x => !x.IsDeleted) // Chỉ lấy records chưa bị xóa
            .AsNoTracking(); // Tối ưu performance

        // Áp dụng filter theo userId
        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        // Áp dụng filter theo productId
        if (productId.HasValue)
            query = query.Where(x => x.ProductId == productId.Value);

        // Áp dụng sorting theo thời gian unbox (mới nhất trước)
        query = query.OrderByDescending(x => x.UnboxedAt);

        // Đếm tổng số records
        var count = await query.CountAsync();

        // Áp dụng pagination
        List<BlindBoxUnboxLog> items;
        if (param.PageIndex == 0) // Trả về tất cả nếu PageIndex = 0
        {
            items = await query.Take(100).ToListAsync(); // Giới hạn tối đa 100 records
        }
        else
        {
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();
        }

        // Map sang DTO
        var dtos = items.Select(x => new UnboxLogDto
        {
            Id = x.Id,
            CustomerBlindBoxId = x.CustomerBlindBoxId,
            CustomerName = x.User?.FullName ?? "N/A",
            ProductId = x.ProductId,
            ProductName = x.ProductName,
            Rarity = x.Rarity,
            DropRate = x.DropRate,
            RollValue = x.RollValue,
            UnboxedAt = x.UnboxedAt,
            BlindBoxName = x.BlindBoxName ?? "N/A",
            Reason = x.Reason ?? "N/A"
        }).ToList();

        var result = new Pagination<UnboxLogDto>(dtos, count, param.PageIndex, param.PageSize);


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
        var totalProbability = probabilities.Values.Sum();

        // HEADER SECTION - Chuyên nghiệp hơn
        sb.AppendLine("# 📋 Báo Cáo Kết Quả Mở Hộp");
        sb.AppendLine();
        sb.AppendLine($"**Thời gian:** `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine($"**Hộp ID:** `{selectedItem.BlindBoxId}`");
        sb.AppendLine();

        // TECHNICAL INFO SECTION
        sb.AppendLine("## 🔧 Thông Số Kỹ Thuật");
        sb.AppendLine();
        sb.AppendLine("| Tham số | Giá trị | Ghi chú |");
        sb.AppendLine("|---------|---------|---------|");
        sb.AppendLine($"| **Random Seed** | `{Math.Round(roll, 6)}` | Giá trị ngẫu nhiên sinh ra |");
        sb.AppendLine($"| **Tổng xác suất** | `{Math.Round(totalProbability, 4)}%` | Tổng tỷ lệ của tất cả items |");
        sb.AppendLine($"| **Thuật toán** | `Weighted Random` | Phương pháp chọn item |");
        sb.AppendLine();

        // PROBABILITY DISTRIBUTION TABLE
        sb.AppendLine("## 📊 Bảng Phân Phối Xác Suất");
        sb.AppendLine();
        sb.AppendLine("| # | Product ID | Tên Sản Phẩm | Rarity | Drop Rate (%) | Range | Status |");
        sb.AppendLine("|---|------------|---------------|--------|---------------|-------|--------|");

        var index = 1;
        decimal cumulative = 0;

        foreach (var kvp in probabilities
                     .OrderByDescending(p => p.Value)
                     .ThenBy(p => p.Key.ProductId))
        {
            var start = cumulative;
            var end = start + kvp.Value;
            cumulative = end;

            var productId = kvp.Key.ProductId.ToString("N")[..8] + "...";
            var itemName = kvp.Key.Product?.Name ?? "NULL";
            var rarity = GetRarityBadge(kvp.Key.RarityConfig?.Name.ToString());
            var dropRate = Math.Round(kvp.Value, 4);
            var range = $"`{Math.Round(start, 4)} - {Math.Round(end, 4)}`";
            var status = kvp.Key.Id == selectedItem.Id
                ? "✅ **SELECTED**"
                : "⚫";

            sb.AppendLine($"| {index} | `{productId}` | {itemName} | {rarity} | `{dropRate}%` | {range} | {status} |");
            index++;
        }

        sb.AppendLine();

        // SELECTION RESULT
        sb.AppendLine("## 🎯 Kết Quả Lựa Chọn");
        sb.AppendLine();
        sb.AppendLine("### Selected Item Details");
        sb.AppendLine();
        sb.AppendLine($"- **Product ID:** `{selectedItem.ProductId}`");
        sb.AppendLine($"- **Item ID:** `{selectedItem.Id}`");
        sb.AppendLine($"- **Product Name:** `{selectedItem.Product?.Name ?? "NULL"}`");
        sb.AppendLine($"- **Configured Drop Rate:** `{Math.Round(selectedItem.DropRate, 4)}%`");
        sb.AppendLine($"- **Rarity Level:** {GetRarityBadge(selectedItem.RarityConfig?.Name.ToString())}");
        sb.AppendLine($"- **Roll Hit Range:** `{GetHitRange(probabilities, selectedItem)}`");

        // VALIDATION SECTION
        sb.AppendLine();
        sb.AppendLine("## ✅ Validation Check");
        sb.AppendLine();
        sb.AppendLine("| Tiêu chí | Kết quả | Status |");
        sb.AppendLine("|----------|---------|--------|");
        sb.AppendLine(
            $"| **Probability Sum** | `{Math.Round(totalProbability, 4)}%` | {(Math.Abs(totalProbability - 100) < 0.01m ? "✅ Valid" : "⚠️ Warning")} |");
        sb.AppendLine($"| **Roll in Valid Range** | `0 ≤ {roll} ≤ {totalProbability}` | ✅ Valid |");
        sb.AppendLine($"| **Item Selection** | Algorithm executed | ✅ Success |");

        // TECHNICAL NOTES
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**⚠️ Lưu ý kỹ thuật:**");
        sb.AppendLine("- Log này chỉ dành cho mục đích kiểm tra và debug");
        sb.AppendLine("- Không chia sẻ thông tin này với khách hàng");
        sb.AppendLine("- Liên hệ team dev nếu có bất thường trong thuật toán");

        return sb.ToString();
    }

// Helper methods
    private string GetRarityBadge(string rarity)
    {
        return rarity?.ToLower() switch
        {
            "common" => "🟢 `COMMON`",
            "uncommon" => "🟡 `UNCOMMON`",
            "rare" => "🔵 `RARE`",
            "epic" => "🟣 `EPIC`",
            "legendary" => "🟠 `LEGENDARY`",
            "mythic" => "🔴 `MYTHIC`",
            _ => "⚫ `UNKNOWN`"
        };
    }

    private string GetHitRange(Dictionary<BlindBoxItem, decimal> probabilities, BlindBoxItem selectedItem)
    {
        decimal cumulative = 0;
        foreach (var kvp in probabilities.OrderByDescending(p => p.Value).ThenBy(p => p.Key.ProductId))
        {
            var start = cumulative;
            var end = start + kvp.Value;

            if (kvp.Key.Id == selectedItem.Id) return $"{Math.Round(start, 4)} - {Math.Round(end, 4)}";

            cumulative = end;
        }

        return "N/A";
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
            CreatedBy = userId,
            OrderDetailId = customerBox.OrderDetailId
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
                new NotificationDto
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