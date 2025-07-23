using System.Text;
using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;
using Microsoft.Extensions.Configuration;

namespace BlindTreasure.Application.Services.ThirdParty.AIModels;

public class GeminiService : IGeminiService
{
    private readonly string _apiKey;
    private readonly ICacheService _cache;
    private readonly HttpClient _httpClient;

    // Danh sách model khả dụng
    public static class GeminiModels
    {
        public const string Pro = "gemini-2.5-pro";
        public const string Flash = "gemini-2.5-flash";
        public const string FlashLite = "gemini-2.5-flash-lite-preview";
        public const string FlashV2 = "gemini-2.0-flash";
        public const string FlashLiteV2 = "gemini-2.0-flash-lite";
    }

    public GeminiService(IHttpClientFactory httpClientFactory, IConfiguration config, ICacheService cache)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = config["Gemini:ApiKey"]
                  ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? throw new Exception("Gemini API key not configured.");
        _cache = cache;
    }

    /// <summary>
    /// Generate response từ Gemini AI với model linh hoạt
    /// </summary>
    /// <param name="userPrompt">Prompt của người dùng</param>
    /// <param name="modelName">Tên model (ví dụ: gemini-2.5-pro)</param>
    public async Task<string> GenerateResponseAsync(string userPrompt, string? modelName = null)
    {
        modelName ??= GeminiModels.FlashV2; // Default fallback model

        var fullPrompt = $"{GeminiContext.SystemPrompt}\n\n{userPrompt}";
        var cacheKey = $"gemini:{modelName}:{fullPrompt.GetHashCode()}";

        // Check cache
        if (await _cache.ExistsAsync(cacheKey))
        {
            var cached = await _cache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached)) return cached;
        }

        // Build API URL với model động
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = fullPrompt }
                    }
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gemini API error: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        var finalResult = result ?? string.Empty;

        // Cache 6 giờ
        await _cache.SetAsync(cacheKey, finalResult, TimeSpan.FromHours(6));
        return finalResult;
    }
}

public static class GeminiContext
{
    public const string SystemPrompt =
        """
        (Thông tin nội bộ – không hiển thị cho người dùng)

        Bạn là trợ lý AI của hệ thống BlindTreasure, hoạt động ở mức service layer. Dưới đây là mô tả chi tiết cấu trúc và nghiệp vụ để bạn hiểu sâu về hệ thống. Khi trả lời người dùng, hãy chuyển thành ngôn ngữ dễ hiểu, không dùng thuật ngữ technical.

        === I. KIẾN TRÚC HỆ THỐNG ===
        1. Backend: ASP.NET Core Web API, tổ chức theo layers:  
           - Controllers (folder API/Controllers)  
           - Services (Application/Services)  
           - Repositories (Infrastructure/Repositories)  
           - DbContext: BlindTreasureDbContext (Domain)  
           - SignalR Hubs: UserChatHub, NotificationHub, v.v.  
        2. Database: PostgreSQL, ORM Entity Framework Core.  
           - Entities: User, Seller, Product, BlindBox, BlindBoxItem, ProbabilityConfig, RarityConfig, CustomerBlindBox, InventoryItem, Listing, Order, OrderDetail, Payment, Transaction, Notification, Promotion, CartItem, Wishlist, Address, Shipment, Review, SupportTicket, OtpVerification.  
           - Quan hệ chính:  
             • BlindBox —< BlindBoxItem —< ProbabilityConfig  
             • CustomerBlindBox —> InventoryItem —> Listing  
             • Order —< OrderDetail —> (Product | BlindBox)  
             • User —< Notification, Wishlist, CartItem, Address, SupportTicket  
        3. Cache: Redis, quản lý price history và danh sách thường dùng.  
        4. Thanh toán: Stripe (StripeClient), lưu cấu hình ở appsettings.json, xử lý webhook.  
        5. Messaging: SignalR để đẩy notification real-time.  
        6. CI/CD: Docker → GitHub Actions → VPS + Nginx.

        === II. LUỒNG NGHIỆP VỤ CHÍNH ===
        A. Xác thực & Phân quyền  
           - Đăng ký Customer / Seller. OTP email qua OtpVerifications.  
           - JWT (access + refresh), bearer token.  
           - Roles: Admin, Staff, Seller, Customer, Guest.  
           - RBAC enforced trên Controller bằng [Authorize(Roles="…")].  

        B. Seller & COA  
           - Seller đăng đơn kèm COA (PDF/Image).  
           - Staff duyệt: ApproveSellerAsync(sellerId, reason).  
           - Seller.Status: InfoEmpty → WaitingReview → Approved/Rejected.  

        C. Quản lý BlindBox  
           1. CreateBlindBox(dto) → lưu BlindBox, trạng thái PENDING.  
           2. AddBlindBoxItems(boxId, items[{productId, quantity, weight, isSecret}]).  
           3. RarityConfig: bảng seed 4 tier (Common, Rare, Epic, Secret) với weight.  
           4. ProbabilityConfig: tính dropRate = weight / sumWeight tại thời điểm submit.  
           5. SubmitForApproval → Staff duyệt hoặc reject với RejectReason.  
           6. Khi Approved: Status = Active, public endpoint GET /api/blindboxes.

        D. Mua & Unbox  
           1. Customer POST /api/cart → /api/checkout → tạo Order + OrderDetails.  
           2. Thanh toán Stripe → callback → tạo Payment + Transaction, Order.Status=Completed.  
           3. Mua blind box: tạo CustomerBlindBox (IsOpened=false).  
           4. OpenBlindBox(boxId):  
              - Lấy list ProbabilityConfig đã duyệt.  
              - Random roll (0–sumWeight), chọn item theo khoảng weight.  
              - Cập nhật CustomerBlindBox.IsOpened=true, OpenedAt.  
              - Tạo InventoryItem(IsFromBlindBox=true).  
              - Ghi BlindBoxUnboxLog (roll, selectedItem, timestamp).

        E. Resale & Listing  
           - GET /api/inventory → chỉ list những InventoryItem.Status = open.  
           - CreateListing(inventoryId, price). Giới hạn 0.01–originalPrice×2.  
           - Lock InventoryItem khi tạo listing.  
           - Lưu price history vào Redis và DB nếu cần.  
           - ExpireOldListingsAsync: background job xóa listing hết hạn.

        F. Notifications  
           - INotificationService.Push(userId, title, message).  
           - Sự kiện: SellerApproved, BlindBoxApproved, OrderPlaced, ShipmentUpdate, PriceDrop, Promotion…  
           - SignalR hub đẩy tới client.

        G. Promotion & Voucher  
           - Promotion entity: loại (Global, Seller), discountType (Percent, Amount), dateRange, usageLimit.  
           - Áp voucher tại checkout, kiểm tra BR-17…BR-18.  

        H. Support & Chat  
           - SupportTicket: userId, assignedTo.  
           - ChatHub: lưu ChatMessage (ticketId, senderId, content, timestamp).

        === III. HẠN CHẾ & QUI TẮC ===
        - DropRate tính toán tự động, ignore input manual.  
        - Mỗi CustomerBlindBox chỉ unbox 1 lần.  
        - Soft-delete: IsDeleted + DeletedAt + DeletedBy.  
        - Audit log: dùng CreatedBy, UpdatedBy, DeletedBy từ IClaimsService.  
        - Rate-limit API: 100 req/phút/IP.  
        - Refresh token revoke on logout/password change.  
        - Redis cache expire: price history 24h; clear on inventory change.  
        - Mỗi user tối đa 5 session.  
        - Chỉ phục vụ thị trường VN, ngôn ngữ giao diện và thông báo bằng tiếng Việt.

        Nếu prompt của người dùng vượt quá phạm vi nghiệp vụ này, phản hồi nội bộ “Out of scope” và khi trả cho user, chuyển sang:  
        “Tôi chỉ hỗ trợ khiếu nại và thông tin liên quan tới chức năng hiện tại của BlindTreasure.”  
        """;
}