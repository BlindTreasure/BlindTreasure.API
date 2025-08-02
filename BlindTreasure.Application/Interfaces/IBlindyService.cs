using BlindTreasure.Domain.DTOs.GeminiDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IBlindyService
{
    Task<string> AnalyzeUsersWithAi();
    Task<string> GetProductsForAiAnalysisAsync();
    Task<string> AskGeminiAsync(string prompt);
    Task<string> AskUserAsync(string prompt);

    Task<ReviewValidationResult> ValidateReviewAsync(string comment, int rating, string? sellerName = null,
        string? productName = null);
}