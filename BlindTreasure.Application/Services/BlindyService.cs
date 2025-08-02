using System.Text;
using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;
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

    public async Task<ReviewValidationResult> ValidateReviewAsync(string comment, int rating, string? sellerName = null,
        string? productName = null)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine(
            "Phân tích và validate nội dung review sau đây cho nền tảng thương mại điện tử BlindTreasure:");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"**Nội dung review:** \"{comment}\"");
        promptBuilder.AppendLine($"**Đánh giá:** {rating}/5 sao");
        promptBuilder.AppendLine($"**Sản phẩm:** {productName ?? "Không rõ"}");
        promptBuilder.AppendLine($"**Seller:** {sellerName ?? "Không rõ"}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Kiểm tra các tiêu chí sau:");
        promptBuilder.AppendLine("1. **Ngôn từ độc hại:** Có chửi thề, xúc phạm, hate speech không?");
        promptBuilder.AppendLine("2. **Spam/Fake:** Có dấu hiệu review ảo, copy-paste, không liên quan không?");
        promptBuilder.AppendLine("3. **Thông tin cá nhân:** Có tiết lộ số điện thoại, email, địa chỉ cụ thể không?");
        promptBuilder.AppendLine("4. **Nội dung bất hợp pháp:** Có quảng cáo sản phẩm khác, link lừa đảo không?");
        promptBuilder.AppendLine("5. **Tính nhất quán:** Rating có phù hợp với nội dung comment không?");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Trả về JSON format:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("    \"isValid\": true/false,");
        promptBuilder.AppendLine("    \"confidence\": 0.0-1.0,");
        promptBuilder.AppendLine("    \"issues\": [\"danh sách vấn đề phát hiện\"],");
        promptBuilder.AppendLine("    \"suggestedAction\": \"approve/moderate/reject\",");
        promptBuilder.AppendLine("    \"cleanedComment\": \"nội dung đã được làm sạch (nếu có)\",");
        promptBuilder.AppendLine("    \"reason\": \"lý do cụ thể\"");
        promptBuilder.AppendLine("}");

        var prompt = promptBuilder.ToString();
        var aiResponse = await _geminiService.GenerateResponseAsync(prompt);

        try
        {
            var result = JsonSerializer.Deserialize<ReviewValidationResult>(aiResponse);
            return result;
        }
        catch
        {
            return new ReviewValidationResult
            {
                IsValid = false,
                Confidence = 0.5,
                Issues = new[] { "AI validation error" },
                SuggestedAction = "moderate",
                Reason = "Không thể phân tích được nội dung"
            };
        }
    }

    /// <summary>
    ///     HÀM NÀY ĐỂ STAFF GỌI CHO AI PHÂN TÍCH HỆ THỐNG
    /// </summary>
    public async Task<string> AnalyzeUsersWithAi()
    {
        var users = await _analyzerService.GetUsersForAiAnalysisAsync();

        var formatted = string.Join("\n", users.Select(u =>
            $"""
             - ID: {u.UserId}
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
             - ID: {p.Id}
               Tên: {p.Name}
               Mô tả: {p.Description}
               Giá: {p.Price} VNĐ
               Tồn kho: {p.Stock}
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