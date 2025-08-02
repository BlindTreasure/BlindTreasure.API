namespace BlindTreasure.Domain.DTOs.GeminiDTOs;

public class ReviewValidationResult
{
    public bool IsValid { get; set; }
    public double Confidence { get; set; }
    public string[] Issues { get; set; } = Array.Empty<string>();
    public string SuggestedAction { get; set; } // approve, moderate, reject
    public string? CleanedComment { get; set; }
    public string Reason { get; set; }
}