using System.Drawing;
using System.Text;
using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.DTOs.UnboxLogDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace BlindTreasure.Application.Services;

public class UnboxingService : IUnboxingService
{
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly ILoggerService _loggerService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<UnboxingHub> _notificationHub;
    private readonly IUserService _userService;
    private readonly IBlindBoxService _blindBoxService;
    private readonly IEmailService _emailService;

    public UnboxingService(ILoggerService loggerService, IUnitOfWork unitOfWork, IClaimsService claimsService,
        ICurrentTime currentTime, INotificationService notificationService, IHubContext<UnboxingHub> notificationHub,
        IUserService userService, IBlindBoxService blindBoxService, IEmailService emailService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _notificationService = notificationService;
        _notificationHub = notificationHub;
        _userService = userService;
        _blindBoxService = blindBoxService;
        _emailService = emailService;
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
            throw ErrorHelper.BadRequest("Rất tiếc, hộp này đã hết vật phẩm. Vui lòng quay lại sau.");

        // 3 & 4. Random item theo xác suất (dùng hàm mới)
        var (selectedItem, roll, probabilityMap) = GetRandomItemByProbability(items, now);
        if (selectedItem == null)
            throw ErrorHelper.Internal("Đã có lỗi xảy ra khi chọn vật phẩm từ hộp. Vui lòng thử lại.");

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
            throw ErrorHelper.Internal("Đã có lỗi xảy ra khi chọn vật phẩm từ hộp. Vui lòng thử lại.");

        await GrantUnboxedItemToUser(selectedItem, customerBox, userId, now);

        var unboxLog = new UnboxLogDto
        {
            CustomerBlindBoxId = customerBox.Id,
            CustomerName = (await _unitOfWork.Users.GetByIdAsync(userId))?.FullName ?? "Anonymous",
            ProductId = selectedItem.ProductId,
            ProductName = selectedItem.Product?.Name ?? "Unknown",
            Rarity = selectedItem.RarityConfig?.Name ?? RarityName.Common,
            DropRate = selectedItem.DropRate,
            UnboxedAt = now,
            BlindBoxName = blindBox?.Name ?? "Unknown"
        };

        // Gửi thông báo real-time
        await _notificationHub.Clients.All.SendAsync("ReceiveUnboxingNotification", unboxLog);


        return new UnboxResultDto
        {
            ProductId = selectedItem.ProductId,
            Rarity = selectedItem.RarityConfig?.Name,
            DropRate = selectedItem.DropRate,
            Weight = selectedItem.RarityConfig?.Weight ?? 0,
            UnboxedAt = now
        };
    }

    public async Task<MemoryStream> ExportToExcelStream(ExportUnboxLogRequest request)
    {
        // Nếu request giữ nguyên mặc định paging => coi như không phân trang
        PaginationParameter? paging = null;
        if (!(request.PageIndex == 1 && request.PageSize == 5 && request.Desc == true)) paging = request;

        var logs = await GetLogsForExportAsync(
            paging,
            request.UserId,
            request.ProductId,
            request.FromDate,
            request.ToDate);

        ExcelPackage.License.SetNonCommercialPersonal("your-name-or-organization");

        using (var package = new ExcelPackage())
        {
            var worksheet = package.Workbook.Worksheets.Add("UnboxingLogs");
            worksheet.DefaultColWidth = 20;

            string[] columnHeaders =
                { "CustomerName", "ProductName", "Rarity", "DropRate", "RollValue", "UnboxedAt", "BlindBoxName" };

            for (var i = 0; i < columnHeaders.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = columnHeaders[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                worksheet.Cells[1, i + 1].Style.Font.Color.SetColor(Color.Black);
                worksheet.Cells[1, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            for (var i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                worksheet.Cells[i + 2, 1].Value = log.CustomerName;
                worksheet.Cells[i + 2, 2].Value = log.ProductName;
                worksheet.Cells[i + 2, 3].Value = log.Rarity;
                worksheet.Cells[i + 2, 4].Value = log.DropRate;
                worksheet.Cells[i + 2, 5].Value = log.RollValue;
                worksheet.Cells[i + 2, 6].Value = log.UnboxedAt.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cells[i + 2, 7].Value = log.BlindBoxName;
            }

            using (var range = worksheet.Cells[1, 1, logs.Count + 1, columnHeaders.Length])
            {
                range.AutoFitColumns();
                range.Style.Font.Name = "Calibri";
                range.Style.Font.Size = 12;
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            }

            var stream = new MemoryStream();
            package.SaveAs(stream);
            if (stream.CanSeek) stream.Position = 0;
            return stream;
        }
    }

    private async Task<List<UnboxLogDto>> GetLogsForExportAsync(PaginationParameter? param, Guid? userId,
        Guid? productId, DateTime? fromDate, DateTime? toDate)
    {
        var query = _unitOfWork.BlindBoxUnboxLogs.GetQueryable()
            .Include(x => x.User)
            .Where(x => !x.IsDeleted)
            .AsNoTracking();

        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId);

        // Seller filter giống GetLogsAsync: nếu current user là Seller thì chỉ lấy logs liên quan tới seller đó
        if (user != null && user.RoleName == RoleType.Seller)
        {
            var seller = await _unitOfWork.Sellers.GetQueryable()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId);

            if (seller != null)
                query = query.Where(x => _unitOfWork.Products.GetQueryable()
                    .Any(p => p.Id == x.ProductId && p.SellerId == seller.Id));
            else
                return new List<UnboxLogDto>();
        }

        // Filter theo userId/productId nếu có
        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (productId.HasValue)
            query = query.Where(x => x.ProductId == productId.Value);

        // Filter theo fromDate/toDate nếu có (inclusive)
        if (fromDate.HasValue)
            query = query.Where(x => x.UnboxedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.UnboxedAt <= toDate.Value);

        // Sort mới nhất trước
        query = query.OrderByDescending(x => x.UnboxedAt);

        // Nếu client truyền paging (param != null && param.PageIndex > 0) => áp dụng paging
        List<BlindBoxUnboxLog> items;
        if (param != null && param.PageIndex > 0)
        {
            var pageSize = param.PageSize > 0 ? param.PageSize : 50;
            items = await query
                .Skip((param.PageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        else
        {
            // Mặc định xuất hết dữ liệu — BE CẢNH BÁO: nếu bảng quá lớn có thể ảnh hưởng performance
            items = await query.ToListAsync();
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

        return dtos;
    }

    public async Task<Pagination<UnboxLogDto>> GetLogsAsync(PaginationParameter param, Guid? userId, Guid? productId)
    {
        var query = _unitOfWork.BlindBoxUnboxLogs.GetQueryable()
            .Include(x => x.User)
            .Where(x => !x.IsDeleted) // Chỉ lấy records chưa bị xóa
            .AsNoTracking(); // Tối ưu performance

        var currentUserId = _claimsService.CurrentUserId; // Lấy UserId từ claims
        var user = await _userService.GetUserById(currentUserId);

        // Kiểm tra nếu user là Seller và áp dụng filter theo SellerId
        if (user != null && user.RoleName == RoleType.Seller)
        {
            // Lấy SellerId từ bảng Seller dựa trên UserId
            var seller = await _unitOfWork.Sellers.GetQueryable()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId);

            if (seller != null)
                // Lọc các BlindBoxUnboxLog theo SellerId thông qua ProductId
                query = query.Where(x => _unitOfWork.Products.GetQueryable()
                    .Any(p => p.Id == x.ProductId && p.SellerId == seller.Id));
            else
                // Nếu không tìm thấy Seller, trả về một query rỗng để không trả về dữ liệu nào
                return new Pagination<UnboxLogDto>();
        }

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
            items = await query.Take(100).ToListAsync(); // Giới hạn tối đa 100 records
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

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

        // ANSI escape codes for colors
        const string reset = "\x1b[0m";
        const string green = "\x1b[32m";
        const string yellow = "\x1b[33m";
        const string cyan = "\x1b[36m";

        // HEADER SECTION
        sb.AppendLine("# Báo Cáo Kết Quả Mở Hộp");
        sb.AppendLine();
        sb.AppendLine($"**Thời gian:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Hộp ID:** {selectedItem.BlindBoxId}");
        sb.AppendLine();

        // TECHNICAL INFO SECTION
        sb.AppendLine("## Thông Số Kỹ Thuật");
        sb.AppendLine();
        sb.AppendLine($"- **Random Seed:** {Math.Round(roll, 6)} (Giá trị ngẫu nhiên sinh ra)");
        sb.AppendLine($"- **Tổng xác suất:** {Math.Round(totalProbability, 4)}% (Tổng tỷ lệ của tất cả items)");
        sb.AppendLine($"- **Thuật toán:** Weighted Random (Phương pháp chọn item)");
        sb.AppendLine();

        // PROBABILITY DISTRIBUTION LIST
        sb.AppendLine("## Phân Phối Xác Suất");
        sb.AppendLine();

        var index = 1;
        decimal cumulative = 0;

        foreach (var kvp in probabilities
                     .OrderByDescending(p => p.Value)
                     .ThenBy(p => p.Key.ProductId))
        {
            var start = cumulative;
            var end = start + kvp.Value;
            cumulative = end;

            var itemName = kvp.Key.Product?.Name ?? "NULL";
            var rarity = GetRarityBadge(kvp.Key.RarityConfig?.Name.ToString());
            var dropRate = Math.Round(kvp.Value, 4);
            var range = $"{Math.Round(start, 4)} - {Math.Round(end, 4)}";

            // Highlight selected item
            if (kvp.Key.Id == selectedItem.Id)
                sb.AppendLine($"{cyan}- **{index}. Sản phẩm: {itemName} (ĐÃ CHỌN){reset}**");
            else
                sb.AppendLine($"- {index}. Sản phẩm: {itemName}");

            sb.AppendLine($"  - Độ hiếm: {rarity}");
            sb.AppendLine($"  - Tỷ lệ Drop: {dropRate}%");
            sb.AppendLine($"  - Range: {range}");
            sb.AppendLine();

            index++;
        }

        // SELECTION RESULT
        sb.AppendLine("## Kết Quả Lựa Chọn");
        sb.AppendLine();
        sb.AppendLine("### Chi Tiết Sản Phẩm Được Chọn");
        sb.AppendLine();
        sb.AppendLine($"- **Product Name:** {selectedItem.Product?.Name ?? "NULL"}");
        sb.AppendLine($"- **Configured Drop Rate:** {Math.Round(selectedItem.DropRate, 4)}%");
        sb.AppendLine($"- **Rarity Level:** {GetRarityBadge(selectedItem.RarityConfig?.Name.ToString())}");
        sb.AppendLine($"- **Roll Hit Range:** {GetHitRange(probabilities, selectedItem)}");
        sb.AppendLine();

        // VALIDATION SECTION
        sb.AppendLine("## Kiểm Tra Validation");
        sb.AppendLine();
        sb.AppendLine(
            $"- **Tổng xác suất:** {Math.Round(totalProbability, 4)}% ({(Math.Abs(totalProbability - 100) < 0.01m ? $"{green}Hợp lệ{reset}" : $"{yellow}Cảnh báo{reset}")})");
        sb.AppendLine($"- **Roll trong khoảng hợp lệ:** 0 ≤ {roll} ≤ {totalProbability} ({green}Hợp lệ{reset})");
        sb.AppendLine($"- **Lựa chọn Item:** Thuật toán đã thực thi ({green}Thành công{reset})");
        sb.AppendLine();

        // TECHNICAL NOTES
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**Lưu ý kỹ thuật:**");
        sb.AppendLine("- Log này chỉ dành cho mục đích kiểm tra và debug");
        sb.AppendLine("- Không chia sẻ thông tin này với khách hàng");
        sb.AppendLine("- Liên hệ team dev nếu có bất thường trong thuật toán");

        return sb.ToString();
    }


// Helper methods
    private string GetRarityBadge(string? rarity)
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
            .ThenInclude(bb => bb!.BlindBoxItems)
            .ThenInclude(bbi => bbi.ProbabilityConfigs)
            .Include(cb => cb.BlindBox)
            .ThenInclude(bb => bb!.BlindBoxItems)
            .ThenInclude(bbi => bbi.Product)
            .FirstOrDefaultAsync(cb => cb.Id == id);

        if (customerBox == null || customerBox.UserId != userId || customerBox.IsDeleted || customerBox.IsOpened)
        {
            var msg = customerBox == null
                ? "Không tìm thấy hộp của bạn. Vui lòng kiểm tra lại."
                : customerBox.IsDeleted
                    ? "Hộp này không còn hợp lệ hoặc đã bị xóa."
                    : customerBox.IsOpened
                        ? "Bạn đã mở hộp này rồi. Mỗi hộp chỉ được mở một lần."
                        : "Bạn không có quyền thực hiện hành động này với hộp của người khác.";
            throw ErrorHelper.BadRequest(msg);
        }

        var itemIds = customerBox.BlindBox!.BlindBoxItems.Select(i => i.Id).ToList();
        var rarities = await _unitOfWork.RarityConfigs.GetQueryable()
            .Where(r => itemIds.Contains(r.BlindBoxItemId))
            .ToListAsync();

        foreach (var item in customerBox.BlindBox.BlindBoxItems)
            item.RarityConfig = rarities.FirstOrDefault(r => r.BlindBoxItemId == item.Id)!;

        return customerBox;
    }


    /// <summary>
    /// Cấp vật phẩm mở được cho user, trừ stock, tính lại drop rate và thêm vào inventory
    /// </summary>
    private async Task GrantUnboxedItemToUser(
        BlindBoxItem selectedItem,
        CustomerBlindBox customerBox,
        Guid userId,
        DateTime now)
    {
        // 1. Giảm quantity của item vừa unbox
        selectedItem.Quantity--;

        // 2. Nếu blindbox tồn tại → cập nhật drop rate sau khi stock thay đổi
        if (customerBox.BlindBox != null)
        {
            // 2.1. Tính toán lại drop rate của toàn bộ items trong box
            await UpdateDropRatesAfterUnboxingAsync(customerBox.BlindBox);

            // 2.2. Nếu item vừa unbox hết số lượng → notify out of stock
            if (selectedItem.Quantity == 0)
                await NotifyOutOfStockAsync(customerBox.BlindBox, selectedItem);
        }

        // 3. Lấy địa chỉ mặc định của user để set location cho inventory item
        var defaultAddress = await _unitOfWork.Addresses.GetQueryable()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault && !a.IsDeleted);

        // 4. Tạo inventory item mới để thêm vào kho của user
        var inventory = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = selectedItem.ProductId,
            UserId = userId,
            Location = defaultAddress?.Province ?? "HCM",
            Status = InventoryItemStatus.Available,
            AddressId = defaultAddress?.Id,
            IsFromBlindBox = true,
            SourceCustomerBlindBoxId = customerBox.Id,
            Tier = selectedItem.RarityConfig.Name,
            CreatedAt = now,
            CreatedBy = userId,
            OrderDetailId = customerBox.OrderDetailId
        };

        // 5. Đánh dấu hộp đã được mở
        customerBox.IsOpened = true;
        customerBox.OpenedAt = now;

        // 6. Lưu thay đổi vào DB
        await _unitOfWork.InventoryItems.AddAsync(inventory);
        await _unitOfWork.CustomerBlindBoxes.Update(customerBox);
        await _unitOfWork.BlindBoxItems.Update(selectedItem);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Cập nhật DropRate cho tất cả item trong BlindBox sau khi stock thay đổi
    /// </summary>
    /// <summary>
    /// Cập nhật DropRate cho tất cả item trong BlindBox sau khi stock thay đổi
    /// - Nếu có item Common hết hàng => disable box + gửi email cho Seller
    /// - Nếu còn stock => tính toán lại DropRate dựa trên Quantity và Weight
    /// </summary>
    private async Task UpdateDropRatesAfterUnboxingAsync(BlindBox blindBox)
    {
        // 1. Load toàn bộ items trong box (bao gồm RarityConfig + Product để log)
        var items = await _unitOfWork.BlindBoxItems.GetQueryable()
            .Include(i => i.RarityConfig)
            .Include(i => i.Product)
            .Where(i => i.BlindBoxId == blindBox.Id && !i.IsDeleted)
            .ToListAsync();

        if (!items.Any())
            return;

        // 2. Check rule: nếu có item Common mà hết hàng (Quantity == 0) → disable box
        var commonItem = items.FirstOrDefault(i => i.RarityConfig != null
                                                   && i.RarityConfig.Name == RarityName.Common
                                                   && i.Quantity == 0);
        if (commonItem != null)
        {
            blindBox.Status = BlindBoxStatus.Disabled;
            await _unitOfWork.BlindBoxes.Update(blindBox);
            await _unitOfWork.SaveChangesAsync();

            // Lấy seller để gửi email thông báo cập nhật stock
            var sellerUser = await _unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Id == blindBox.Seller.UserId);

            if (sellerUser != null)
            {
                await _emailService.SendCommonItemOutOfStockAsync(
                    sellerUser.Email,
                    sellerUser.FullName ?? sellerUser.Email,
                    blindBox.Name,
                    commonItem.Product?.Name ?? "Unknown Product"
                );
            }

            _loggerService.Warn(
                $"[DropRate] BlindBox {blindBox.Id} bị disable vì Common '{commonItem.Product?.Name}' hết hàng."
            );
            return;
        }

        // 3. Log DropRate trước khi cập nhật
        var sbBefore = new StringBuilder();
        sbBefore.AppendLine($"[DropRate-BEFORE] BlindBox {blindBox.Id}:");
        foreach (var item in items.OrderByDescending(x => x.DropRate))
        {
            sbBefore.AppendLine($"- {item.Product?.Name ?? "Unknown"} | " +
                                $"Rarity: {item.RarityConfig?.Name} | " +
                                $"Qty: {item.Quantity} | " +
                                $"DropRate: {item.DropRate:N2}%");
        }

        _loggerService.Info(sbBefore.ToString());

        // 4. Convert sang DTO để gọi CalculateDropRates trong BlindBoxService
        var dtoItems = items.Select(i => new BlindBoxItemRequestDto
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            Weight = i.RarityConfig?.Weight ?? 1
        }).ToList();

        // 5. Gọi BlindBoxService.CalculateDropRates để tính toán lại drop rates
        var dropRates = _blindBoxService.CalculateDropRates(dtoItems);

        // 6. Map kết quả DropRate mới vào các BlindBoxItem trong DB
        foreach (var kvp in dropRates)
        {
            var dto = kvp.Key;
            var newRate = kvp.Value;

            var target = items.FirstOrDefault(x => x.ProductId == dto.ProductId);
            if (target != null)
            {
                target.DropRate = newRate;
                await _unitOfWork.BlindBoxItems.Update(target);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        // 7. Log DropRate sau khi cập nhật
        var sbAfter = new StringBuilder();
        sbAfter.AppendLine($"[DropRate-AFTER] BlindBox {blindBox.Id}:");
        foreach (var item in items.OrderByDescending(x => x.DropRate))
        {
            sbAfter.AppendLine($"- {item.Product?.Name ?? "Unknown"} | " +
                               $"Rarity: {item.RarityConfig?.Name} | " +
                               $"Qty: {item.Quantity} | " +
                               $"DropRate: {item.DropRate:N2}%");
        }

        _loggerService.Info(sbAfter.ToString());
    }


    private async Task NotifyOutOfStockAsync(BlindBox blindBox, BlindBoxItem item)
    {
        blindBox.Status = BlindBoxStatus.Rejected;
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