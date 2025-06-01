namespace BlindTreasure.Application.Interfaces;

public interface IBlindyService
{
    Task<string> AskGeminiAsync(string prompt);

    Task<string> AskCustomerAsync(string prompt);
    Task<string> AskSellerAsync(string prompt);
    Task<string> AskGuestAsync(string prompt);
    Task<string> AskStaffAsync(string prompt);
}