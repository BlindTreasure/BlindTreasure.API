using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;

namespace BlindTreasure.Application.Services;

public class BlindyService : IBlindyService
{
    private readonly IGeminiService _geminiService;
    private readonly IUserService _userService;

    public BlindyService(IGeminiService geminiService, IUserService userService)
    {
        _geminiService = geminiService;
        _userService = userService;
    }

    
    /// <summary>
    /// HÀM NÀY ĐỂ STAFF GỌI CHO AI PHÂN TÍCH HỆ THỐNG
    /// </summary>
    public async Task<string> AnalyzeUsersWithAi()
    {
        var users = await _userService.GetUsersForAiAnalysisAsync();

        var formatted = string.Join("\n", users.Select(u =>
            $"""
             - ID: {u.UserId}
               Họ tên: {u.FullName}
               Email: {u.Email}
               Số điện thoại: {u.PhoneNumber}
               Avatar: {u.AvatarUrl}
               Ngày sinh: {(u.DateOfBirth?.ToString("yyyy-MM-dd") ?? "Không có")}
               Giới tính: {(u.Gender.HasValue ? (u.Gender.Value ? "Nam" : "Nữ") : "Không rõ")}
               Trạng thái: {u.Status}
               Vai trò: {u.RoleName}
               Ngày tạo: {u.CreatedAt:yyyy-MM-dd HH:mm}
             """
        ));


        var prompt = $"""
                          Dưới đây là danh sách 100 user gần nhất trong hệ thống BlindTreasure:
                          {formatted}

                          Phân tích:
                          - Trạng thái nào đang phổ biến?
                          - Có tỷ lệ email @gmail.com vượt trội không?
                          - Có user nào có tên hoặc email đáng nghi?
                          - Có dấu hiệu bot/clone/ảo nào không?
                          - Gợi ý những điều staff nên kiểm tra thêm?

                          Trả lời súc tích, ưu tiên bullet point.
                      """;
        return await AskStaffAsync(prompt);
    }

    #region AskRoles

    public async Task<string> AskGeminiAsync(string prompt)
    {
        return await _geminiService.GenerateResponseAsync(prompt);
    }

    public async Task<string> AskCustomerAsync(string prompt)
    {
        var fullPrompt = $"""
                              Bạn là AI hỗ trợ khách hàng trên nền tảng BlindTreasure. Trả lời ngắn gọn, đúng trọng tâm về việc mua, mở box và bán lại item đã mở.

                              {prompt}
                          """;

        return await _geminiService.GenerateResponseAsync(fullPrompt);
    }

    public async Task<string> AskSellerAsync(string prompt)
    {
        var fullPrompt = $"""
                              Bạn là AI hỗ trợ Seller trên BlindTreasure. Hướng dẫn chi tiết quy trình tạo sản phẩm thường, cấu hình blind box và upload COA cho từng sản phẩm. Trả lời ngắn gọn, rõ ràng.
                              {prompt}
                          """;
        return await _geminiService.GenerateResponseAsync(fullPrompt);
    }

    public async Task<string> AskGuestAsync(string prompt)
    {
        var fullPrompt = $"""
                              Bạn là AI hỗ trợ Guest trên BlindTreasure. Hướng dẫn ngắn gọn, rõ ràng về cách đăng ký tài khoản và đăng ký trở thành Seller chính thức.
                              {prompt}
                          """;
        return await _geminiService.GenerateResponseAsync(fullPrompt);
    }

    public async Task<string> AskStaffAsync(string prompt)
    {
        var fullPrompt = $"""
                              Bạn là AI nội bộ hỗ trợ nghiệp vụ dành cho nhân viên (staff) nền tảng BlindTreasure. Ưu tiên trả lời ngắn gọn, súc tích, đúng quy trình và nghiệp vụ quản trị.

                              {prompt}
                          """;
        return await _geminiService.GenerateResponseAsync(fullPrompt);
    }

    #endregion
}