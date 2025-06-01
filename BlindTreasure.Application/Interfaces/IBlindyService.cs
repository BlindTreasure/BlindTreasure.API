namespace BlindTreasure.Application.Interfaces;

public interface IBlindyService
{
    Task<string> AskGeminiAsync(string prompt);
    
    Task<string> AskCustomerAsync(string prompt);

    // Task<string> AskGptAsync(string prompt);
}