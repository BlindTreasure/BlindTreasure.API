namespace BlindTreasure.Application.Interfaces;

public interface IBlindyService
{
    Task<string> AskGeminiAsync(string prompt);

    Task<string> AskUserAsync(string prompt);
    Task<string> AskSellerAsync(string prompt);
    Task<string> AskStaffAsync(string prompt);
}