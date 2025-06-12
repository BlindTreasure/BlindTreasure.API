namespace BlindTreasure.Domain.DTOs;

public class UpdateBlindBoxDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public DateTime ReleaseDate { get; set; }
    public bool HasSecretItem { get; set; }
    public int SecretProbability { get; set; }
}
