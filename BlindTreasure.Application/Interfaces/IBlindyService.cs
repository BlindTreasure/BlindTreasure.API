
namespace BlindTreasure.Application.Interfaces;

public interface IBlindyService
{
    Task<string> AnalyzeUsersWithAi();
    Task<string> GetProductsForAiAnalysisAsync();
    Task<string> AskGeminiAsync(string prompt);
    Task<string> AskUserAsync(string prompt);
    // Task<bool> ValidateReviewAsync(string comment);
}