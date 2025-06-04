namespace BlindTreasure.Domain.DTOs.BlindBoxDTOs;

public class BlindBoxDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public string ImageUrl { get; set; }
    public DateTime ReleaseDate { get; set; }

    public string Status { get; set; } // ví dụ: Draft, PendingApproval, Approved...

    public bool HasSecretItem { get; set; }
    public decimal SecretProbability { get; set; }

    public List<BlindBoxItemDto> Items { get; set; }
}