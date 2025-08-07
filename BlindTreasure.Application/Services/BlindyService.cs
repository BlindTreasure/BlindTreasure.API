using System.Text;
using System.Text.Json;
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

        // AI validation prompt using StringBuilder with AppendLine
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Phân tích nội dung đánh giá sản phẩm sau đây và trả về JSON format:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("    \"isValid\": boolean, // true nếu nội dung phù hợp");
        promptBuilder.AppendLine("    \"reasons\": string[] // lý do nếu không hợp lệ");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Tiêu chí đánh giá:");
        promptBuilder.AppendLine("- Không chứa ngôn từ tục tĩu, thô lỗ");
        promptBuilder.AppendLine("- Không chứa thông tin cá nhân");
        promptBuilder.AppendLine("- Không spam, quảng cáo");
        promptBuilder.AppendLine("- Nội dung liên quan đến trải nghiệm sản phẩm");
        promptBuilder.AppendLine("- Không chứa liên kết hoặc thông tin liên hệ");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Nội dung cần kiểm tra:");
        promptBuilder.AppendLine(comment);

        var prompt = promptBuilder.ToString();

        try
        {
            var response = await _geminiService.GenerateValidationResponseAsync(prompt);
            var result = JsonSerializer.Deserialize<GeminiValidationResponse>(response);

            return result?.IsValid ?? false;
        }
        catch
        {
            // Fallback to basic validation if AI fails
            return BasicTextValidation(comment);
        }
    }

    private bool BasicTextValidation(string text)
    {
        // Basic profanity filter fallback
        var bannedWords = new[] { "fuck", "shit", "địt", "đụ", "lồn", "cặc" }; // Add more as needed
        return !bannedWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private class GeminiValidationResponse
    {
        public bool IsValid { get; set; }
        public string[] Reasons { get; set; } = Array.Empty<string>();
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