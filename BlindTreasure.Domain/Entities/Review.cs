namespace BlindTreasure.Domain.Entities;

public class Review : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    // Có thể là Product hoặc BlindBox
    public Guid? ProductId { get; set; }
    public Product Product { get; set; }
    public Guid? BlindBoxId { get; set; }
    public BlindBox BlindBox { get; set; }

    public int Rating { get; set; }
    public string Comment { get; set; }
    public bool IsPublished { get; set; }
}