using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;

namespace BlindTreasure.Application.Services;

public class BlindyService : IBlindyService
{
    private readonly IGeminiService _geminiService;

    public BlindyService(IGeminiService geminiService)
    {
        _geminiService = geminiService;
    }

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
    
    
}