using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;

namespace BlindTreasure.Application.Services;

public class CurrencyConversionService : ICurrencyConversionService
{
    private const string CurrencyFreaksUrl = "https://api.currencyfreaks.com/v2.0/rates/latest";
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public CurrencyConversionService(string apiKey = "a2ac8e200bfb44d39c48c5f54f28b29b")
    {
        _httpClient = new HttpClient();
        _apiKey = apiKey;
    }

    public async Task<decimal?> GetVNDToUSDRate()
    {
        var url = $"{CurrencyFreaksUrl}?apikey={_apiKey}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) throw ErrorHelper.BadRequest(ErrorMessages.Currency_APIFailed);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("rates", out var rates) && rates.TryGetProperty("VND", out var vndRateElement))
            if (decimal.TryParse(vndRateElement.GetString(), out var vndRate))
                return vndRate; // Stripe prefers 2 decimal places

        return null;
    }
}