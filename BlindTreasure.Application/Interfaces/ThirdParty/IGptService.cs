namespace BlindTreasure.Application.Interfaces.ThirdParty;

public interface IGptService
{
    Task<string> GenerateResponseAsync(string prompt);
}