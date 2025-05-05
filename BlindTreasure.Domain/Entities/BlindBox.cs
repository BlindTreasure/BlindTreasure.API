namespace BlindTreasure.Domain.Entities;

public class BlindBox : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public string Title { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int TotalItems { get; set; }
    public string ImageUrl { get; set; }
    public string Status { get; set; }
    public bool IsSecret { get; set; }

    public ICollection<BlindBoxItem> BlindBoxItems { get; set; }
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<WishlistItem> WishlistItems { get; set; }
}