namespace BlindTreasure.Domain.DTOs.ReviewDTOs;

public class ReviewResponseDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string? UserAvatarUrl { get; set; }
    public string? ProductName { get; set; }
    public string? BlindBoxName { get; set; }
    public string SellerName { get; set; }
    public int OverallRating { get; set; }
    public int? QualityRating { get; set; }
    public int? ServiceRating { get; set; }
    public int? DeliveryRating { get; set; }
    public string Comment { get; set; }
    public List<string> ImageUrls { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public string? SellerResponse { get; set; }
    public DateTime? SellerResponseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; }
}

// ReviewValidationResult.cs (cho AI)
public class ReviewValidationResult
{
    public bool IsValid { get; set; }
    public double Confidence { get; set; }
    public string[] Issues { get; set; } = Array.Empty<string>();
    public string SuggestedAction { get; set; } // approve, moderate, reject
    public string? CleanedComment { get; set; }
    public string Reason { get; set; }
}