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
    public DbSet<RarityConfig> RarityConfigs { get; set; }
    public DbSet<ProbabilityConfig> ProbabilityConfigs { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<CustomerBlindBox> CustomerBlindBoxes { get; set; }
    public DbSet<CustomerFavourite> CustomerFavourites { get; set; }
    public DbSet<OtpVerification> OtpVerifications { get; set; }
    public DbSet<Listing> Listings { get; set; }
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
    public DbSet<PromotionParticipant> PromotionParticipants { get; set; }

    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<BlindBoxUnboxLog> BlindBoxUnboxLogs { get; set; }
    public DbSet<ListingReport> ListingReports { get; set; }
    public DbSet<TradeRequest> TradeRequests { get; set; }
    public DbSet<TradeRequestItem> TradeRequestItems { get; set; }
    public DbSet<TradeHistory> TradeHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region enums converter

        modelBuilder.Entity<ChatMessage>()
            .Property(c => c.MessageType)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.SenderType)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.ReceiverType)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<Listing>(entity =>
        {
            entity.Property(l => l.Status)
                .HasConversion<string>() // Enum sẽ lưu dưới dạng chuỗi
                .HasMaxLength(32);
        });


        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.Property(e => e.Purpose)
                .HasConversion<string>() // enum -> string
                .HasMaxLength(32); // giới hạn độ dài nếu cần
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.Property(e => e.CreatedByRole)
                .HasConversion<string>() // enum -> string
                .HasMaxLength(32); // giới hạn độ dài nếu cần
        });

        modelBuilder.Entity<BlindBox>(entity =>
        {
            entity.Property(e => e.Status)
                .HasConversion<string>() // enum -> string
                .HasMaxLength(32); // giới hạn độ dài nếu cần
        });

        modelBuilder.Entity<RarityConfig>(entity =>
        {
            entity.Property(e => e.Name)
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

        modelBuilder.Entity<PromotionParticipant>(entity =>
        {
            entity.HasKey(pp => pp.Id);

            entity.HasOne(pp => pp.Promotion)
                .WithMany(p => p.PromotionParticipants)
                .HasForeignKey(pp => pp.PromotionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pp => pp.Seller)
                .WithMany(s => s.PromotionParticipants)
                .HasForeignKey(pp => pp.SellerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(pp => new { pp.PromotionId, pp.SellerId }).IsUnique();
        });

        #endregion

        modelBuilder.Entity<ListingReport>(entity =>
        {
            entity.Property(r => r.Reason)
                .HasMaxLength(500)
                .IsRequired();

            entity.HasOne(r => r.Listing)
                .WithMany()
                .HasForeignKey(r => r.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerFavourite>(entity =>
        {
            entity.Property(cf => cf.Type)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            // Relationship với User
            entity.HasOne(cf => cf.User)
                .WithMany(u => u.CustomerFavourites)
                .HasForeignKey(cf => cf.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship với Product (optional)
            entity.HasOne(cf => cf.Product)
                .WithMany(p => p.CustomerFavourites)
                .HasForeignKey(cf => cf.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship với BlindBox (optional)
            entity.HasOne(cf => cf.BlindBox)
                .WithMany(b => b.CustomerFavourites)
                .HasForeignKey(cf => cf.BlindBoxId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: một user chỉ có thể thích một item một lần
            entity.HasIndex(cf => new { cf.UserId, cf.ProductId, cf.BlindBoxId })
                .IsUnique()
                .HasFilter("\"ProductId\" IS NOT NULL OR \"BlindBoxId\" IS NOT NULL");

            // Check constraint: phải có ít nhất một trong ProductId hoặc BlindBoxId
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_CustomerFavourite_OneTypeOnly",
                "(\"ProductId\" IS NOT NULL AND \"BlindBoxId\" IS NULL) OR (\"ProductId\" IS NULL AND \"BlindBoxId\" IS NOT NULL)"));
        });

        modelBuilder.Entity<BlindBoxUnboxLog>(entity =>
        {
            entity.Property(x => x.Rarity)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(e => e.CustomerName)
                .HasMaxLength(255)
                .IsRequired(false); // Vì có thể null

            entity.Property(e => e.ProductName)
                .HasMaxLength(255)
                .IsRequired(); // Required vì không có ?

            entity.Property(e => e.ProbabilityTableJson)
                .HasColumnType("jsonb"); // PostgreSQL JSONB type
            // Không cần MaxLength cho JSONB

            entity.Property(e => e.BlindBoxName)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Reason)
                .HasColumnType("text") // Cho phép text dài
                .IsRequired(); // Required vì có default value

            // Navigation property
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false); // vì AI không phải user

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);


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


        modelBuilder.Entity<Product>()
            .Property(p => p.ImageUrls)
            .HasConversion(
                v => string.Join(";", v),
                v => v.Split(";", StringSplitOptions.RemoveEmptyEntries).ToList()
            ).IsRequired(false);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(n => n.Type)
                .HasConversion<string>() // Lưu dưới dạng chuỗi trong DB
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(n => n.Type)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(n => n.Title)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(n => n.Message)
                .HasMaxLength(500)
                .IsRequired();
        });


        modelBuilder.Entity<CustomerBlindBox>(entity =>
        {
            // Khóa ngoại: User (1-n)
            entity.HasOne(ci => ci.User)
                .WithMany(u => u.CustomerBlindBoxes)
                .HasForeignKey(ci => ci.UserId)
                .OnDelete(DeleteBehavior.Cascade); // hoặc Restrict tùy nhu cầu

            // Khóa ngoại: BlindBox (1-n)
            entity.HasOne(ci => ci.BlindBox)
                .WithMany(b => b.CustomerBlindBoxes)
                .HasForeignKey(ci => ci.BlindBoxId)
                .OnDelete(DeleteBehavior.Restrict); // tránh xóa hộp → mất lịch sử

            // Khóa ngoại: OrderDetail (1-n), optional
            entity.HasOne(ci => ci.OrderDetail)
                .WithMany(od => od.CustomerBlindBoxes)
                .HasForeignKey(ci => ci.OrderDetailId)
                .OnDelete(DeleteBehavior.SetNull); // mất đơn hàng vẫn giữ lịch sử hộp
            // Cấu hình các cột
            entity.Property(ci => ci.IsOpened)
                .IsRequired();
        });

        modelBuilder.Entity<BlindBox>(entity =>
        {
            entity.Property(b => b.Name)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(b => b.Description)
                .HasMaxLength(1000);

            entity.Property(b => b.ImageUrl)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(b => b.BindBoxTags)
                .HasMaxLength(1000);

            entity.Property(b => b.Brand)
                .HasMaxLength(255);

            entity.Property(b => b.RejectReason)
                .HasMaxLength(1000);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.Property(ii => ii.Location)
                .HasMaxLength(100);

            entity.Property(ii => ii.Status)
                .HasMaxLength(50);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.Property(ii => ii.Location)
                .HasMaxLength(100);

            entity.Property(ii => ii.Status)
                .HasConversion<string>() // enum lưu dạng string
                .HasMaxLength(50)
                .IsRequired();

            entity.HasOne(ii => ii.Address)
                .WithMany()
                .HasForeignKey(ii => ii.AddressId)
                .OnDelete(DeleteBehavior.SetNull);
        });


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

        modelBuilder.Entity<BlindBoxItem>()
            .HasOne(bi => bi.RarityConfig)
            .WithOne(rc => rc.BlindBoxItem)
            .HasForeignKey<RarityConfig>(rc => rc.BlindBoxItemId)
            .OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<Review>(entity =>
        {
            entity.Property(r => r.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(r => r.ImageUrls)
                .HasConversion(
                    v => string.Join(";", v),
                    v => v.Split(";", StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            entity.Property(r => r.OriginalComment)
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(r => r.ProcessedComment)
                .HasMaxLength(2000);

            entity.Property(r => r.ValidationReason)
                .HasMaxLength(500);

            entity.Property(r => r.AiValidationDetails)
                .HasColumnType("jsonb");

            // Relationships...
            entity.HasOne(r => r.OrderDetail)
                .WithMany(od => od.Reviews)
                .HasForeignKey(r => r.OrderDetailId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Seller)
                .WithMany(s => s.Reviews)
                .HasForeignKey(r => r.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: 1 OrderDetail chỉ có 1 review
            entity.HasIndex(r => r.OrderDetailId).IsUnique();
        });

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

        modelBuilder.Entity<TradeRequest>(entity =>
        {
            entity.Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            // Thêm cấu hình cho TimeRemaining
            entity.Property(t => t.TimeRemaining)
                .HasDefaultValue(0);
            
            entity.HasOne(t => t.Listing)
                .WithMany()
                .HasForeignKey(t => t.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Requester)
                .WithMany()
                .HasForeignKey(t => t.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TradeHistory>(entity =>
        {
            entity.Property(t => t.FinalStatus)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasOne(t => t.Listing)
                .WithMany()
                .HasForeignKey(t => t.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Requester)
                .WithMany()
                .HasForeignKey(t => t.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.OfferedInventory)
                .WithMany()
                .HasForeignKey(t => t.OfferedInventoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(16);
        });


        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<OrderDetail>()
            .HasMany(od => od.Shipments)
            .WithMany(s => s.OrderDetails)
            .UsingEntity(j => j.ToTable("OrderDetailShipments"));
    }
}