namespace BlindTreasure.Application.Interfaces.ThirdParty.AIModels;

public interface IGeminiService
{
    /// <summary>
    /// Generate response từ Gemini AI với model linh hoạt
    /// </summary>
    /// <param name="userPrompt">Prompt của người dùng</param>
    /// <param name="modelName">Tên model (ví dụ: gemini-2.5-pro)</param>
    Task<string> GenerateResponseAsync(string userPrompt, string? modelName = null);

    Task<string> GenerateValidationResponseAsync(string userPrompt);

    string GenerateFallbackValidation(string prompt);
}