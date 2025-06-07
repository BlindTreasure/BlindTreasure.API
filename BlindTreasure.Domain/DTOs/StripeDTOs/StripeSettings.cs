namespace BlindTreasure.Domain.DTOs.StripeDTOs;

public class StripeSettings
{
    public string PublishableKey { get; set; }
    public string SecretKey { get; set; }
    public string WebhookSecret { get; set; }
    public string ClientId { get; set; }
    public string ApiVersion { get; set; } = "2020-08-27"; // Default API version
    public string AppName { get; set; } = "BlindTreasure"; // Default app name
}