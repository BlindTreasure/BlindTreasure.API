using BlindTreasure.Application.Interfaces.ThirdParty;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace BlindTreasure.Application.Services.ThirdParty;

public class GptService : IGptService
{
    private readonly OpenAIService _openAiService;

    public GptService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        _openAiService = new OpenAIService(new OpenAiOptions
        {
            ApiKey = apiKey
        });
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("Bạn là trợ lý thương mại thông minh."),
                ChatMessage.FromUser(prompt)
            },
            Model = Models.Gpt_3_5_Turbo_16k
        });

        if (completionResult.Successful)
            return completionResult.Choices.First().Message.Content ?? throw new InvalidOperationException();

        throw new Exception(completionResult.Error?.Message ?? "GPT failed");
    }
}