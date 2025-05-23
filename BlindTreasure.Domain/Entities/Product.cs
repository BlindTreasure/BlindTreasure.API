﻿namespace BlindTreasure.Domain.Entities;

public class Product : BaseEntity
{
    // FK → Seller
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }

    // FK → Category
    public Guid CategoryId { get; set; }
    public Category Category { get; set; }

    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string ImageUrl { get; set; }
    public string Status { get; set; }

    // 1-n → Certificates, BlindBoxItems, CartItems, OrderDetails, WishlistItems, Reviews
    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<BlindBoxItem> BlindBoxItems { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; }
    public ICollection<CartItem> CartItems { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; }
    public ICollection<WishlistItem> WishlistItems { get; set; }
    public ICollection<Review> Reviews { get; set; }
}