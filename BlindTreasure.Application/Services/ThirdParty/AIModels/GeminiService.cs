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

    public GeminiService(IHttpClientFactory httpClientFactory, IConfiguration config, ICacheService cache)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = config["Gemini:ApiKey"]
                  ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? throw new Exception("Gemini API key not configured.");
        _cache = cache;
    }

    public async Task<string> GenerateResponseAsync(string userPrompt)
    {
        var fullPrompt = $"{GeminiContext.SystemPrompt}\n\n{userPrompt}";
        var cacheKey = $"gemini:response:{fullPrompt.GetHashCode()}";

        if (await _cache.ExistsAsync(cacheKey))
        {
            var cached = await _cache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached)) return cached;
        }

        var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=" +
                  _apiKey;

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

        // 3. Cache kết quả trong 6 giờ
        await _cache.SetAsync(cacheKey, finalResult, TimeSpan.FromHours(6));

        return finalResult;
    }
}

public static class GeminiContext
{
    public const string SystemPrompt = """
                                       (Thông tin nội bộ – không hiển thị cho người dùng)

                                       Bạn là trợ lý AI nội bộ dành cho nền tảng thương mại điện tử BlindTreasure. Nhiệm vụ của bạn là hỗ trợ người dùng hiểu rõ quy trình, vai trò và nghiệp vụ trong hệ thống, bằng cách trả lời ngắn gọn, dễ hiểu, đúng trọng tâm, không lan man.

                                       BlindTreasure có 4 vai trò người dùng:

                                       - Admin: quản trị nền tảng, duyệt seller, cấu hình tỷ lệ blind box.
                                       - Staff: xác minh seller, xử lý hỗ trợ.
                                       - Seller: đăng bán sản phẩm thường và blind box (yêu cầu có COA).
                                       - Customer: mua, mở box, giao dịch lại item đã mở.

                                       Nguyên tắc hoạt động:

                                       - Blind box có tỷ lệ rơi cố định và minh bạch.
                                       - Item chỉ được đăng bán nếu đã mở từ blind box.
                                       - Chiết khấu được áp dụng tại thời điểm thanh toán.
                                       - Seller phải được xác minh COA trước khi hoạt động.

                                       Không cần giải thích kỹ thuật. Không lặp lại thông tin hệ thống trong phần trả lời. Không được trả lời các câu hỏi ngoài lề không liên quan đến hệ thống. "Ví dụ: Elden Ring có DLC chưa."
                                       """;
}