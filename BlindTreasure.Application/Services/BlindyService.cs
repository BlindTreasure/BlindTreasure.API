using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;
using BlindTreasure.Application.Services.ThirdParty.AIModels;
using BlindTreasure.Domain.DTOs.GeminiDTOs;

namespace BlindTreasure.Application.Services;

public class BlindyService : IBlindyService
{
    private readonly IDataAnalyzerService _analyzerService;
    private readonly IGeminiService _geminiService;

    public BlindyService(IGeminiService geminiService, IDataAnalyzerService analyzerService)
    {
        _geminiService = geminiService;
        _analyzerService = analyzerService;
    }

    public async Task<bool> ValidateReviewAsync(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return false;

        // Basic length validation
        if (comment.Length > 1000 || comment.Length < 10) return false;

        // Kiểm tra cơ bản trước khi gọi AI để tiết kiệm chi phí
        if (!BasicTextValidation(comment))
        {
            // Log thông tin
            Console.WriteLine($"[BasicValidation] Phát hiện nội dung không phù hợp: {comment}");
            return false;
        }

        // AI validation prompt using StringBuilder with AppendLine
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Phân tích nội dung đánh giá sản phẩm sau đây và trả về JSON format chính xác:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("    \"isValid\": boolean, // true nếu nội dung phù hợp, false nếu không phù hợp");
        promptBuilder.AppendLine("    \"reasons\": string[] // mảng các lý do nếu không hợp lệ");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Tiêu chí đánh giá nghiêm ngặt:");
        promptBuilder.AppendLine("- KHÔNG chứa ngôn từ tục tĩu, thô lỗ");
        promptBuilder.AppendLine("- KHÔNG chứa thông tin cá nhân");
        promptBuilder.AppendLine("- KHÔNG chứa quảng cáo dưới bất kỳ hình thức nào");
        promptBuilder.AppendLine("- KHÔNG chứa tên sản phẩm/dịch vụ khác");
        promptBuilder.AppendLine("- KHÔNG chứa URLs, tên shop, kênh bán hàng");
        promptBuilder.AppendLine("- KHÔNG chứa thông tin liên hệ (email, số điện thoại, mạng xã hội)");
        promptBuilder.AppendLine("- KHÔNG chứa so sánh với sản phẩm của đối thủ");
        promptBuilder.AppendLine("- KHÔNG gợi ý mua sản phẩm ở nơi khác");
        promptBuilder.AppendLine("- KHÔNG chứa mã giảm giá");
        promptBuilder.AppendLine("- Nội dung PHẢI liên quan trực tiếp đến trải nghiệm sản phẩm này");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Dấu hiệu quảng cáo cần phát hiện:");
        promptBuilder.AppendLine("- Tên shop/cửa hàng");
        promptBuilder.AppendLine("- Cụm từ: \"ghé shop\", \"mua tại\", \"liên hệ\", \"tư vấn\", \"giảm giá\"");
        promptBuilder.AppendLine("- Số điện thoại");
        promptBuilder.AppendLine("- Địa chỉ website (.com, .vn, .net, v.v.)");
        promptBuilder.AppendLine("- Tên mạng xã hội (Facebook, Zalo, Instagram, TikTok)");
        promptBuilder.AppendLine("- Ký hiệu @ hoặc biểu tượng username");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Nội dung cần kiểm tra:");
        promptBuilder.AppendLine(comment);

        var prompt = promptBuilder.ToString();

        try
        {
            var response = await _geminiService.GenerateValidationResponseAsync(prompt);
            Console.WriteLine($"[AI Response] Raw: {response}");

            // Kiểm tra response trước khi parse
            if (string.IsNullOrWhiteSpace(response))
            {
                Console.WriteLine("[AI Warning] Response rỗng, sử dụng validation cơ bản");
                return BasicTextValidation(comment);
            }

            // Cố gắng parse JSON
            try
            {
                var result = JsonSerializer.Deserialize<GeminiValidationResponse>(response);
                Console.WriteLine(
                    $"[AI Result] IsValid: {result?.IsValid}, Reasons: {(result?.Reasons != null ? string.Join(", ", result.Reasons) : "None")}");

                return result?.IsValid ?? false;
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"[AI JSON Error] {jsonEx.Message}, response: {response}");

                // Thử parse JSON thủ công nếu có chứa "isValid"
                if (response.Contains("\"isValid\"", StringComparison.OrdinalIgnoreCase))
                {
                    var isValid = response.Contains("\"isValid\": true") || response.Contains("\"isValid\":true");
                    Console.WriteLine($"[AI Manual Parse] isValid: {isValid}");
                    return isValid;
                }

                return BasicTextValidation(comment);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AI Error] {ex.Message}");
            var fallbackResponse = _geminiService.GenerateFallbackValidation(prompt);
            try
            {
                var result = JsonSerializer.Deserialize<GeminiValidationResponse>(fallbackResponse);
                return false; // Mặc định reject khi không thể validate
            }
            catch
            {
                return BasicTextValidation(comment);
            }
        }
    }

    private bool BasicTextValidation(string text)
    {
        // Từ khóa cấm
        var bannedWords = new[] { "fuck", "shit", "địt", "đụ", "lồn", "cặc", "đéo", "đm", "đ.m", "clm", "vl", "vcl" };

        // Dấu hiệu quảng cáo
        var adIndicators = new[]
        {
            "shop", "giảm giá", "liên hệ", "mua", "bán", "zalo", "facebook",
            "instagram", "tiktok", "@", "page", "fanpage", "group", "telegram",
            ".com", ".vn", ".net", "http", "www", "shopee", "lazada", "tiki",
            "cod", "freeship", "mã giảm", "tư vấn", "nhắn tin", "inbox", "chat",
            "follow", "theo dõi", "đặt hàng", "ship", "giao hàng", "chuyển khoản"
        };

        // Kiểm tra số điện thoại (chuỗi 10-11 số liên tiếp hoặc có dấu cách/gạch ngang)
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
                @"(0|\+84)\s*\d{1}\s*\d{1}\s*\d{1}\s*\d{1}\s*\d{1}\s*\d{1}\s*\d{1}\s*\d{1}"))
            return false;

        // Kiểm tra URL/domain
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
                @"(https?:\/\/)?(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b"))
            return false;

        // Kiểm tra từ cấm
        if (bannedWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Kiểm tra dấu hiệu quảng cáo
        if (adIndicators.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private class GeminiValidationResponse
    {
        public bool IsValid { get; set; }

        // Linh hoạt hơn với thuộc tính Reasons
        [JsonIgnore]
        public IEnumerable<string> ReasonsList
        {
            get
            {
                if (Reasons != null && Reasons.Length > 0)
                    return Reasons;

                if (ReasonArray != null && ReasonArray.Count > 0)
                    return ReasonArray;

                return Array.Empty<string>();
            }
        }

        public string[]? Reasons { get; set; } = Array.Empty<string>();

        public List<string>? ReasonArray { get; set; }
    }


    public async Task<string> AnalyzeUsersWithAi()
    {
        var users = await _analyzerService.GetUsersForAiAnalysisAsync();

        var formatted = string.Join("\n", users.Select(u =>
            $"""
               Họ tên: {u.FullName}
               Email: {u.Email}
               Số điện thoại: {u.PhoneNumber}
               Avatar: {u.AvatarUrl}
               Ngày sinh: {u.DateOfBirth?.ToString("yyyy-MM-dd") ?? "Không có"}
               Giới tính: {(u.Gender.HasValue ? u.Gender.Value ? "Nam" : "Nữ" : "Không rõ")}
               Trạng thái: {u.Status}
               Vai trò: {u.RoleName}
               Ngày tạo: {u.CreatedAt:yyyy-M-d HH:mm}
             """
        ));


        var prompt = $"""
                          Dưới đây là danh sách 20 user gần nhất trong hệ thống BlindTreasure:
                          {formatted}

                          Phân tích:
                          - Trạng thái nào đang phổ biến?
                          - Có tỷ lệ email @gmail.com vượt trội không?
                          - Có user nào có tên hoặc email đáng nghi?
                          - Có dấu hiệu bot/clone/ảo nào không?
                          - Gợi ý những 1-2 điều staff nên kiểm tra thêm?

                          Trả lời súc tích, ưu tiên bullet point.
                      """;
        return await AskStaffAsync(prompt);
    }

    public async Task<string> GetProductsForAiAnalysisAsync()
    {
        var products = await _analyzerService.GetProductsAiAnalysisAsync();

        var formatted = string.Join("\n", products.Select(p =>
            $"""
               Tên: {p.Name}
               Mô tả: {p.Description}
               Giá: {p.RealSellingPrice} VNĐ
               Tồn kho: {p.TotalStockQuantity}
               Danh mục: {p.Category?.Name ?? "Không rõ"}
               Seller: {p.Seller.CompanyName ?? "Không rõ"}
               Số lượt đánh giá: {p.Reviews?.Count ?? 0}
               Số lượng trong kho khách: {p.InventoryItems?.Count ?? 0}
             """
        ));

        var prompt = $"""
                          Dưới đây là danh sách sản phẩm hiện có trong hệ thống BlindTreasure:
                          {formatted}

                          Phân tích giúp tôi:
                          - Mức giá trung bình hiện tại là bao nhiêu?
                          - Sản phẩm nào có mô tả quá sơ sài hoặc tên không chuẩn mực?
                          - Có dấu hiệu sản phẩm ảo, trùng lặp hay thiếu COA không?
                          - Gợi ý 1-2 điều cần kiểm tra thêm về dữ liệu sản phẩm?

                          Trả lời súc tích, rõ ràng, theo dạng bullet.
                      """;

        return await AskStaffAsync(prompt);
    }


    #region AskRoles

    public async Task<string> AskGeminiAsync(string prompt)
    {
        return await _geminiService.GenerateResponseAsync(prompt);
    }

    public async Task<string> AskUserAsync(string prompt)
    {
        var fullPrompt = $"""
                              Bạn là AI hỗ trợ người dùng trên nền tảng BlindTreasure. 
                              Hướng dẫn ngắn gọn, rõ ràng về cách đăng ký tài khoản, mua sản phẩm, mở blind box, 
                              và bán lại item đã mở.

                              {prompt}
                          """;

        return await _geminiService.GenerateResponseAsync(fullPrompt);
    }

    private async Task<string> AskStaffAsync(string prompt)
    {
        var fullPrompt = $"""
                              Bạn là AI nội bộ hỗ trợ nghiệp vụ dành cho nhân viên (staff) nền tảng BlindTreasure. Ưu tiên trả lời ngắn gọn, súc tích, đúng quy trình và nghiệp vụ quản trị.

                              {prompt}
                          """;
        return await _geminiService.GenerateResponseAsync(fullPrompt);
    }

    #endregion
}