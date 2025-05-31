namespace BlindTreasure.Application.Interfaces.ThirdParty.AIModels;

public interface IGeminiService
{
    Task<string> GenerateResponseAsync(string prompt);
}