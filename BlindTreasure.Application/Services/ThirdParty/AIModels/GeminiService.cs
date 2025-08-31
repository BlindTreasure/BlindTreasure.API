using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        _cache = cache;
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? throw new Exception("Gemini API key not configured.");
    }

    /// <summary>
    ///     Generate response từ Gemini AI với model linh hoạt
    /// </summary>
    /// <param name="userPrompt">Prompt của người dùng</param>
    /// <param name="modelName">Tên model (ví dụ: gemini-2.5-pro)</param>
    public async Task<string> GenerateResponseAsync(Guid userId, string userPrompt, string? modelName = null)
    {
        modelName ??= GeminiModels.FlashV2;

        // 1) Load memory
        var mem = await LoadMemoryAsync(userId);

        // 2) Build prompt
        var memPreface = BuildMemoryPreface(mem);
        var fullPrompt =
            $"{GeminiContext.SystemPrompt}\n\n{GeminiContext.ResponseRules}\n\n{memPreface}\n\n[USER REQUEST]\n{userPrompt}";

        // 3) Call Gemini
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}";
        var body = new { contents = new[] { new { parts = new[] { new { text = fullPrompt } } } } };

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
        var result = doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString();
        var final = NormalizeNewlines(result ?? "");

        // 4) Update memory
        try
        {
            var patch = await ExtractMemoryPatchAsync(userPrompt);
            ApplyPatch(mem, patch);
            await SaveMemoryAsync(userId, mem);
        }
        catch
        {
            /* bỏ qua nếu update lỗi */
        }

        return final;
    }

    public async Task<string> GenerateValidationResponseAsync(string userPrompt)
    {
        // Sử dụng model nhẹ nhất và context tối thiểu

        var promptBuilder = new StringBuilder();
        promptBuilder.Clear();
        promptBuilder.AppendLine("Bạn là AI validator. Phân tích nội dung đánh giá sản phẩm sau đây.");
        promptBuilder.AppendLine("Trả về CHÍNH XÁC JSON format theo mẫu:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"isValid\": false,");
        promptBuilder.AppendLine("  \"reasons\": [\"Lý do 1\", \"Lý do 2\"]");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Nếu hợp lệ thì isValid = true và reasons = []");
        promptBuilder.AppendLine("KHÔNG được thêm bất kỳ text nào khác ngoài JSON.");

        var fullPrompt = $"{promptBuilder}\n\n{userPrompt}";

        // Sử dụng model nhẹ nhất
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModels.Flash}:generateContent?key={_apiKey}";

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
            },
            generationConfig = new
            {
                temperature = 0.1, // Giảm randomness cho consistency
                maxOutputTokens = 500, // Giới hạn response length
                topP = 0.8,
                topK = 10
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
        finalResult = NormalizeNewlines(finalResult);
        return finalResult;
    }

    public string GenerateFallbackValidation(string prompt)
    {
        // Trả về JSON chuẩn khi API lỗi
        return
            "{\"isValid\": false, \"reasons\": [\"API validation không khả dụng, bài đánh giá cần kiểm duyệt thủ công.\"]}";
    }

    private async Task<AiMemory> LoadMemoryAsync(Guid userId)
    {
        var mem = await _cache.GetAsync<AiMemory>(AiMemKeys.MemoryKey(userId));
        return mem ?? new AiMemory();
    }

    private async Task SaveMemoryAsync(Guid userId, AiMemory mem)
    {
        mem.UpdatedAt = DateTime.UtcNow;
        await _cache.SetAsync(AiMemKeys.MemoryKey(userId), mem, TimeSpan.FromDays(90));
    }

    private static string BuildMemoryPreface(AiMemory mem)
    {
        var facts = mem.Facts?.Count > 0
            ? "- " + string.Join("\n- ", mem.Facts)
            : "- (chưa có)";
        var summary = string.IsNullOrWhiteSpace(mem.Summary) ? "(chưa có)" : mem.Summary;

        return $"""
                [USER PROFILE – dùng nội bộ]
                Summary: {summary}
                Facts:
                {facts}
                """;
    }

    private static void ApplyPatch(AiMemory mem, MemoryPatch patch, int maxFacts = 20)
    {
        if (!patch.ShouldRemember) return;

        var set = new HashSet<string>(mem.Facts ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var f in patch.Facts) set.Add(f.Trim());
        mem.Facts = set.Where(x => !string.IsNullOrWhiteSpace(x)).Take(maxFacts).ToList();

        if (!string.IsNullOrWhiteSpace(patch.SummaryDelta))
        {
            var combined = (mem.Summary + " " + patch.SummaryDelta).Trim();
            mem.Summary = combined.Length > 320 ? combined[..320] : combined;
        }
    }

    private async Task<MemoryPatch> ExtractMemoryPatchAsync(string userUtterance)
    {
        var extractorPrompt = """
                              Bạn là "Memory Extractor". Trích xuất các thông tin nên ghi nhớ lâu dài từ câu nói của user.

                              CHỈ TRẢ VỀ JSON, KHÔNG THÊM TEXT KHÁC. Mẫu:
                              {
                                "shouldRemember": true,
                                "facts": ["...","..."],
                                "summaryDelta": "..."
                              }
                              """;

        var full = extractorPrompt + "\n\n---\nUser said:\n" + userUtterance;

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModels.Flash}:generateContent?key={_apiKey}";
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = full } } } },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 400 }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var res = await _httpClient.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode) return new MemoryPatch();

        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString() ?? "{}";

        return JsonSerializer.Deserialize<MemoryPatch>(text) ?? new MemoryPatch();
    }

    // thêm helper này trong class GeminiService
    private static string NormalizeNewlines(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1) Nếu model in literal "\n" hoặc "\r\n" dưới dạng text, chuyển về newline thật
        input = input.Replace("\\r\\n", "\n").Replace("\\n", "\n");

        // 2) chuẩn hoá loại newline khác
        input = input.Replace("\r\n", "\n").Replace("\r", "\n");

        // 3) xóa khoảng trắng thừa trước/sau newline
        input = Regex.Replace(input, @"[ \t]+\n", "\n");
        input = Regex.Replace(input, @"\n[ \t]+", "\n");

        // 4) collapse nhiều blank line xuống tối đa 1 blank line (tức <= 2 newline liên tiếp)
        input = Regex.Replace(input, @"\n{3,}", "\n\n");

        // 5) trim đầu-cuối
        input = input.Trim();

        return input;
    }


    // Danh sách model khả dụng
    public static class GeminiModels
    {
        public const string Pro = "gemini-2.5-pro";
        public const string Flash = "gemini-2.5-flash";
        public const string FlashLite = "gemini-2.5-flash-lite-preview";
        public const string FlashV2 = "gemini-2.0-flash";
        public const string FlashLiteV2 = "gemini-2.0-flash-lite";
    }
}

public static class GeminiContext
{
    public const string ResponseRules =
        """
        (Quy tắc trả lời – áp dụng cho mọi phản hồi gửi tới user)

        - Luôn trả lời bằng tiếng Việt, ngắn gọn, đúng nghiệp vụ.
        - KHÔNG in ký tự escape '\n' (backslash + n) trong output; nếu cần newline hãy dùng newline thực.
        - Hạn chế xuống dòng: không dùng hơn **1 dòng trống** liên tiếp (tức không có >2 newline liên tiếp).
        - Mỗi đoạn (paragraph) chỉ dùng **1 newline (\n)**. Tuyệt đối không chèn >2 newline liên tiếp.
        - Nếu cần tách mục, ưu tiên dùng bullet (`-`), số thứ tự (`1.`) hoặc **bảng Markdown**; tránh nhiều dòng trống giữa các mục.
        - Độ dài: ưu tiên trả lời **ngắn gọn** — tối đa 6 câu cho phản hồi tiêu chuẩn; với nhiều đơn, dùng bảng.
        - Ưu tiên bullet point khi liệt kê; không lặp lại câu hỏi.
        - Không tiết lộ chi tiết kỹ thuật nội bộ (schema DB, repo, CI/CD, token…).
        - Mẫu trả lời mặc định:
          1) Kết luận / Trả lời trực tiếp
          2) Bước tiếp theo (hành động hoặc màn hình/API)
        - Nếu output chứa literal "\n" (ví dụ model in \\n), hệ thống phải chuyển thành newline thực và tự làm sạch nhiều newline thừa trước khi trả về user.
        - Nếu yêu cầu ngoài phạm vi chức năng đang hỗ trợ hoặc trái quy tắc → chỉ trả lời:
          "Tôi chỉ hỗ trợ khiếu nại và thông tin liên quan tới chức năng hiện tại của BlindTreasure."
        """;


    public const string SystemPrompt =
        """
        (Thông tin nội bộ – không hiển thị cho người dùng)

        Vai trò & Mục tiêu
        - Bạn là trợ lý nghiệp vụ cấp service layer cho BlindTreasure.
        - Trả lời hướng dẫn thao tác, giải thích kết quả theo main flow và Business Rules (BR).
        - Không sáng tạo tính năng mới, không “phá luật”.

        Sự thật hệ thống (tóm tắt bắt buộc)
        - Đăng ký/OTP kích hoạt trước khi dùng tính năng mua bán; RBAC theo vai trò Customer/Seller/Staff/Admin. Chỉ Admin/Staff được tạo Seller; Seller phải được xác minh/COA.  [Flow đăng ký/đăng nhập, vai trò, COA] 
        - Blind Box: Seller khai báo, Staff phê duyệt tỉ lệ; drop-rate tính theo weight đã duyệt; chỉ sau khi Approved mới bán.  [Flow khai báo/phê duyệt; tính xác suất và trạng thái]
        - Mua & thanh toán: cart → checkout → Stripe callback → Order completed; nếu mua Blind Box thì tạo CustomerBlindBox (chưa mở).  [Flow mua/checkout]
        - Unbox: random theo ProbabilityConfig đã duyệt; mỗi hộp mở 1 lần; tạo InventoryItem; chỉ item đã mở mới được listing.  [Flow unbox]
        - Resale/Listing: chỉ inventory open; giá niêm yết bị giới hạn 0.01–originalPrice×2; auto expire listing cũ.  [Flow listing/giới hạn giá]
        - Order workflow: Pending → Processing → Shipped → Delivered → Completed; không cho hủy sau Shipped; timeout 24h nếu chưa thanh toán.  [BR đơn hàng]
        - Promotion/Voucher: kiểm tra hiệu lực + giới hạn sử dụng tại checkout. Refund trong 7 ngày theo policy.  [BR khuyến mãi/thanh toán]
        - Vận chuyển: tạo đơn qua đối tác (GHN) khi chọn giao hàng tận nhà.  [BR vận chuyển]
        - Thông báo realtime qua Notification/SignalR cho các sự kiện (duyệt Seller/BlindBox, đơn hàng, vận chuyển, khuyến mãi…).  [Flow thông báo]
        - Chỉ phục vụ thị trường Việt Nam; giao diện/thông báo tiếng Việt.  [Giới hạn vùng]

        Checklist ra quyết định (bắt buộc trước khi trả lời)
        1) Xác định actor & trạng thái: vai trò (Customer/Seller/Staff/Admin), Seller đã xác minh chưa, Order/Listing/BlindBox đang ở trạng thái nào.
        2) Áp quy tắc tương ứng:
           - Đăng ký/kích hoạt/OTP trước khi truy cập tính năng bảo vệ (Auth & RBAC).
           - Blind Box chỉ bán/giải thích tỉ lệ theo cấu hình đã phê duyệt; không chấp nhận chỉnh tay drop-rate.
           - Unbox duy nhất một lần/hộp; chỉ item đã mở mới được bán lại.
           - Giá listing phải trong biên 0.01–originalPrice×2.
           - Tuân thủ Order workflow; không hủy sau Shipped; timeout 24h nếu chưa trả tiền.
           - Áp mã giảm giá hợp lệ; refund ≤ 7 ngày khi tiêu chí thoả.
           - Nếu người dùng đòi tính năng ngoài phạm vi/khác quy tắc → dùng câu từ chối chuẩn.
        3) Soạn trả lời theo “Mẫu trả lời” ở ResponseRules.

        Phạm vi & Từ chối
        - Không tư vấn ngoài nghiệp vụ thương mại điện tử BlindTreasure (ví dụ: sửa mã nguồn, thiết kế DB, luật/thuế chi tiết…).
        - Không tiết lộ thông tin nội bộ (token, config, webhook, cache, audit).
        - Câu từ chối chuẩn (dùng nguyên văn khi out-of-scope hoặc trái BR):
          "Tôi chỉ hỗ trợ khiếu nại và thông tin liên quan tới chức năng hiện tại của BlindTreasure."
        """;
}

// --- Add: DTO cho memory cơ bản ---
public sealed class AiMemory
{
    public string Summary { get; set; } = ""; // 1–3 câu tóm tắt hồ sơ user
    public List<string> Facts { get; set; } = new(); // Facts bền vững (sở thích, ràng buộc)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class MemoryPatch
{
    public bool ShouldRemember { get; set; }
    public List<string> Facts { get; set; } = new();
    public string? SummaryDelta { get; set; } // bổ sung cho phần tóm tắt
}

// --- Add: helper key ---
internal static class AiMemKeys
{
    public static string MemoryKey(Guid userId)
    {
        return $"ai:mem:{userId}";
    }
}