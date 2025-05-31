namespace BlindTreasure.Application.Interfaces.ThirdParty.AIModels;

public interface IGptService
{
    Task<string> GenerateResponseAsync(string prompt);
}