﻿using BlindTreasure.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Domain;

public class BlindTreasureDbContext : DbContext
{
    public BlindTreasureDbContext()
    {
    }

    public BlindTreasureDbContext(DbContextOptions<BlindTreasureDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Seller> Sellers { get; set; }
    public DbSet<Certificate> Certificates { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<BlindBox> BlindBoxes { get; set; }
    public DbSet<BlindBoxItem> BlindBoxItems { get; set; }
    public DbSet<ProbabilityConfig> ProbabilityConfigs { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }

    public DbSet<OtpVerification> OtpVerifications { get; set; }
    public DbSet<Listing> Listings { get; set; }
    public DbSet<CustomerDiscount> CustomerDiscounts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<Wishlist> Wishlists { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Role ↔ User (1-n)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleName)
            .IsRequired();

        modelBuilder.Entity<User>()
            .Property(u => u.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Role>()
            .Property(r => r.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // --- Cấu hình User.RoleName làm FK trỏ vào Role.Type ---
        modelBuilder.Entity<User>()
            .Property(u => u.RoleName)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleName)
            .HasPrincipalKey(r => r.Type);

        // --- Định nghĩa Alternate Key trên Role.Type ---
        modelBuilder.Entity<Role>()
            .HasAlternateKey(r => r.Type);

        #region MyRegion

        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.Property(e => e.Purpose)
                .HasConversion<string>() // enum -> string
                .HasMaxLength(32); // giới hạn độ dài nếu cần
        });

        modelBuilder.Entity<BlindBox>(entity =>
        {
            entity.Property(e => e.Status)
                .HasConversion<string>() // enum -> string
                .HasMaxLength(32); // giới hạn độ dài nếu cần
        });

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.Property(e => e.Status)
                .HasConversion<string>() // enum -> string
                .HasMaxLength(32); // giới hạn độ dài nếu cần
        });

        modelBuilder.Entity<Product>()
            .Property(p => p.ProductType)
            .HasConversion<string>()
            .HasMaxLength(32); // nếu cần giới hạn


        modelBuilder.Entity<Product>()
            .Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32); // nếu cần giới hạn


        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.Property(e => e.DiscountType)
                .HasConversion<string>()
                .HasMaxLength(16);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(16);
        });

        #endregion


        // BlindBox ↔ Category (n-1)
        modelBuilder.Entity<BlindBox>()
            .HasOne(bb => bb.Category)
            .WithMany()
            .HasForeignKey(bb => bb.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<Product>()
            .Property(p => p.ImageUrls)
            .HasConversion(
                v => string.Join(";", v),
                v => v.Split(";", StringSplitOptions.RemoveEmptyEntries).ToList()
            ).IsRequired(false);

        modelBuilder.Entity<BlindBoxItem>()
            .Property(p => p.Rarity)
            .HasConversion<string>()
            .HasMaxLength(32); // nếu cần giới hạn


        // User ↔ Seller (1-1)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Seller)
            .WithOne(s => s.User)
            .HasForeignKey<Seller>(s => s.UserId);

        // Seller ↔ Certificate (1-n)
        modelBuilder.Entity<Seller>()
            .HasMany(s => s.Certificates)
            .WithOne(c => c.Seller)
            .HasForeignKey(c => c.SellerId);

        // Certificate ↔ VerifiedByUser (1-n, restrict)
        modelBuilder.Entity<Certificate>()
            .HasOne(c => c.VerifiedByUser)
            .WithMany(u => u.VerifiedCertificates)
            .HasForeignKey(c => c.VerifiedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Seller ↔ Product (1-n)
        modelBuilder.Entity<Seller>()
            .HasMany(s => s.Products)
            .WithOne(p => p.Seller)
            .HasForeignKey(p => p.SellerId);

        // Product ↔ Category (n-1)
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId);

        // Category self-reference (1-n, restrict)
        modelBuilder.Entity<Category>()
            .HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seller ↔ BlindBox (1-n)
        modelBuilder.Entity<Seller>()
            .HasMany(s => s.BlindBoxes)
            .WithOne(b => b.Seller)
            .HasForeignKey(b => b.SellerId);

        // BlindBox ↔ BlindBoxItem (1-n)
        modelBuilder.Entity<BlindBox>()
            .HasMany(b => b.BlindBoxItems)
            .WithOne(i => i.BlindBox)
            .HasForeignKey(i => i.BlindBoxId);

        // BlindBoxItem ↔ Product (n-1)
        modelBuilder.Entity<BlindBoxItem>()
            .HasOne(i => i.Product)
            .WithMany(p => p.BlindBoxItems)
            .HasForeignKey(i => i.ProductId);

        // BlindBoxItem ↔ ProbabilityConfig (1-n)
        modelBuilder.Entity<ProbabilityConfig>()
            .HasOne(pc => pc.BlindBoxItem)
            .WithMany(i => i.ProbabilityConfigs)
            .HasForeignKey(pc => pc.BlindBoxItemId);

        // ProbabilityConfig ↔ ApprovedByUser (1-n, restrict)
        modelBuilder.Entity<ProbabilityConfig>()
            .HasOne(pc => pc.ApprovedByUser)
            .WithMany(u => u.ApprovedProbabilityConfigs)
            .HasForeignKey(pc => pc.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // InventoryItem ↔ User (n-1)
        modelBuilder.Entity<InventoryItem>()
            .HasOne(ii => ii.User)
            .WithMany(u => u.InventoryItems)
            .HasForeignKey(ii => ii.UserId);

        // InventoryItem ↔ Product (n-1)
        modelBuilder.Entity<InventoryItem>()
            .HasOne(ii => ii.Product)
            .WithMany(p => p.InventoryItems)
            .HasForeignKey(ii => ii.ProductId);

        // InventoryItem ↔ Listing (1-n)
        modelBuilder.Entity<InventoryItem>()
            .HasMany(ii => ii.Listings)
            .WithOne(l => l.InventoryItem)
            .HasForeignKey(l => l.InventoryId);

        // CustomerDiscount ↔ User (n-1)
        modelBuilder.Entity<CustomerDiscount>()
            .HasOne(cd => cd.Customer)
            .WithMany(u => u.CustomerDiscounts)
            .HasForeignKey(cd => cd.CustomerId);

        // CartItem ↔ User (n-1)
        modelBuilder.Entity<CartItem>()
            .HasOne(ci => ci.User)
            .WithMany(u => u.CartItems)
            .HasForeignKey(ci => ci.UserId);

        // CartItem ↔ Product (n-1) / BlindBox (n-1), set null
        modelBuilder.Entity<CartItem>()
            .HasOne(ci => ci.Product)
            .WithMany(p => p.CartItems)
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CartItem>()
            .HasOne(ci => ci.BlindBox)
            .WithMany(b => b.CartItems)
            .HasForeignKey(ci => ci.BlindBoxId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order ↔ User (n-1)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId);

        // Order ↔ Payment (1-1), set null
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Order>(o => o.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order ↔ Address (n-1), set null
        modelBuilder.Entity<Order>()
            .HasOne(o => o.ShippingAddress)
            .WithMany(a => a.Orders)
            .HasForeignKey(o => o.ShippingAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        // OrderDetail ↔ Order (n-1)
        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(od => od.OrderId);

        // OrderDetail ↔ Product / BlindBox (n-1), set null
        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.Product)
            .WithMany(p => p.OrderDetails)
            .HasForeignKey(od => od.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.BlindBox)
            .WithMany(b => b.OrderDetails)
            .HasForeignKey(od => od.BlindBoxId)
            .OnDelete(DeleteBehavior.SetNull);

        // Payment ↔ Transaction (1-n)
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Payment)
            .WithMany(p => p.Transactions)
            .HasForeignKey(t => t.PaymentId);

        // Address ↔ User (n-1)
        modelBuilder.Entity<Address>()
            .HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId);

        // Shipment ↔ OrderDetail (n-1)
        modelBuilder.Entity<Shipment>()
            .HasOne(s => s.OrderDetail)
            .WithMany(od => od.Shipments)
            .HasForeignKey(s => s.OrderDetailId);

        // Review ↔ User / Product / BlindBox (n-1), set null
        modelBuilder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.BlindBox)
            .WithMany(b => b.Reviews)
            .HasForeignKey(r => r.BlindBoxId)
            .OnDelete(DeleteBehavior.SetNull);

        // SupportTicket ↔ User / AssignedTo (n-1), set null
        modelBuilder.Entity<SupportTicket>()
            .HasOne(st => st.User)
            .WithMany(u => u.SupportTickets)
            .HasForeignKey(st => st.UserId);

        modelBuilder.Entity<SupportTicket>()
            .HasOne(st => st.AssignedToUser)
            .WithMany()
            .HasForeignKey(st => st.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);

        // Notification ↔ User (n-1)
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId);

        // Wishlist ↔ User (n-1)
        modelBuilder.Entity<Wishlist>()
            .HasOne(w => w.User)
            .WithMany(u => u.Wishlists)
            .HasForeignKey(w => w.UserId);

        // WishlistItem ↔ Wishlist / Product / BlindBox (n-1), set null
        modelBuilder.Entity<WishlistItem>()
            .HasOne(wi => wi.Wishlist)
            .WithMany(w => w.WishlistItems)
            .HasForeignKey(wi => wi.WishlistId);

        modelBuilder.Entity<WishlistItem>()
            .HasOne(wi => wi.Product)
            .WithMany(p => p.WishlistItems)
            .HasForeignKey(wi => wi.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<WishlistItem>()
            .HasOne(wi => wi.BlindBox)
            .WithMany(b => b.WishlistItems)
            .HasForeignKey(wi => wi.BlindBoxId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}