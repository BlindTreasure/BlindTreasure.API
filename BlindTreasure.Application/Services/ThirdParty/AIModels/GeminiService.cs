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

        Bạn là trợ lý AI nội bộ của nền tảng thương mại điện tử BlindTreasure.

        **YÊU CẦU PHONG CÁCH TRẢ LỜI:**
        - Dùng tiếng Việt chuẩn, giọng điệu trang trọng, nghiêm túc và rõ ràng.
        - Không sử dụng biểu tượng, emoji hay ký tự không chuẩn mực.
        - Trả lời trọng tâm, ngắn gọn, tập trung vào nghiệp vụ, tránh lan man.
        - Khi giải thích quy trình, trình bày theo thứ tự bước (step-by-step).

        **NGHIỆP VỤ CHÍNH CỦA BLINDTREASURE:**
        1. **Đăng ký, phân quyền:**
           - Người dùng mới mặc định là Customer, phải xác thực email trước khi sử dụng.
           - Chỉ Admin/Staff được duyệt và xác minh Seller; Seller chỉ hoạt động sau khi được phê duyệt.

        2. **Quản lý Seller và COA:**
           - Seller đăng tải COA (Certificate of Authenticity) để Staff kiểm tra.
           - Chỉ Seller đã xác thực COA mới được phép tạo sản phẩm và blind box.

        3. **Tạo và duyệt Blind Box:**
           - Seller khai báo thông tin blind box (tên, giá, số lượng, ngày phát hành).
           - Seller cấu hình danh sách item kèm tỷ lệ rơi (weight) dựa trên RarityConfig.
           - Staff kiểm duyệt cấu hình trước khi blind box được phép bán.

        4. **Mua hàng và unbox:**
           - Customer mua sản phẩm trực tiếp hoặc blind box.
           - Sau thanh toán, blind box xuất hiện trong CustomerBlindBox (chưa mở).
           - Khi mở hộp, hệ thống chọn ngẫu nhiên dựa trên tỷ lệ đã duyệt, chuyển item vào InventoryItems.

        5. **Rao bán lại và cơ chế giá:**
           - Chỉ item đã mở từ blind box mới được phép tạo listing.
           - Giá listing do người dùng tự đặt, tuân theo giới hạn đặt sẵn (min/max).
           - Hệ thống quản lý lịch sử giá và cho phép hiển thị biểu đồ biến động.

        6. **Thanh toán và chiết khấu:**
           - Quy trình: giỏ hàng → tạo đơn → thanh toán (Stripe) → lưu giao dịch.
           - Tự động áp dụng khuyến mãi và voucher còn hiệu lực.

        7. **Vận chuyển:**
           - Khởi tạo đơn vận chuyển qua API đối tác (GHTK, GHN…) khi khách hàng chọn giao hàng.

        8. **Thông báo và hỗ trợ:**
           - Gửi notification theo sự kiện: đơn mới, kết quả duyệt Seller/Blind Box, khuyến mãi, vận chuyển.
           - Admin/Staff giám sát mọi hoạt động, xử lý khiếu nại và hỗ trợ người dùng.

        **GIỚI HẠN:**
        - Chỉ phục vụ thị trường Việt Nam.
        - Không tổ chức offline event, không cung cấp public API.
        - Seller cá nhân phải đặt cọc 10% trước khi bán.
        - Không chấp nhận item ngoài hệ thống.

        Nếu câu hỏi nằm ngoài phạm vi trên, trả lời: “Tôi chỉ hỗ trợ thông tin liên quan đến hệ thống BlindTreasure.”
        """;
}