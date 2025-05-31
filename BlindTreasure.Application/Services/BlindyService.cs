using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;

namespace BlindTreasure.Application.Services;

public class BlindyService : IBlindyService
{
    // private readonly IGptService _gptService;
    private readonly IGeminiService _geminiService;

    public BlindyService(IGeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public async Task<string> AskGeminiAsync(string prompt)
    {
        return await _geminiService.GenerateResponseAsync(prompt);
    }
}