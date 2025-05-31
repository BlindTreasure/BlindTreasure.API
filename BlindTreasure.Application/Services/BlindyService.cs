using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;

namespace BlindTreasure.Application.Services;

public class BlindyService : IBlindyService
{
    private readonly IGptService _gptService;
    private readonly IGeminiService _geminiService;

    public BlindyService(IGptService service, IGeminiService geminiService)
    {
        _gptService = service;
        _geminiService = geminiService;
    }
}