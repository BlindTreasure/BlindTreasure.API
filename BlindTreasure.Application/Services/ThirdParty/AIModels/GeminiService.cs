using System.Text;
using System.Text.Json;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;
using Microsoft.Extensions.Configuration;

namespace BlindTreasure.Application.Services.ThirdParty;

public class GeminiService : IGptService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public GeminiService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _apiKey = configuration["Gemini:ApiKey"];
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        var url =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro-latest:generateContent?key=AIzaSyB2i2jJmpwoUvXboy5N1DLQ91ORTw6cgmQ";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 2048
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gemini API error: {error}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseContent);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return text ?? "No response";
    }
}