namespace BlindTreasure.Domain.Entities;

public class CustomerFavourite : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    // Có thể là Product hoặc BlindBox (chỉ một trong hai)
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? BlindBoxId { get; set; }
    public BlindBox? BlindBox { get; set; }

    // Enum để phân biệt loại yêu thích
    public FavouriteType Type { get; set; }
}

public enum FavouriteType
{
    Product,
    BlindBox
}