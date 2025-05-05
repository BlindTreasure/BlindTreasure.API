using BlindTreasure.Domain.Entities;
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
    public DbSet<VerificationRequest> VerificationRequests { get; set; }
    public DbSet<Certificate> Certificates { get; set; }
    public DbSet<Deposit> Deposits { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<BlindBox> BlindBoxes { get; set; }
    public DbSet<BlindBoxItem> BlindBoxItems { get; set; }
    public DbSet<ProbabilityConfig> ProbabilityConfigs { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<Cart> Carts { get; set; }
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
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasMany(e => e.Users)
                .WithOne(u => u.Role)
                .HasForeignKey(u => u.RoleId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId);
            // 1-1 User ↔ Seller
            entity.HasOne(u => u.Seller)
                .WithOne(s => s.User)
                .HasForeignKey<Seller>(s => s.UserId);
        });

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.ToTable("Sellers");
            entity.HasKey(e => e.Id);

            // 1-1: Seller → Deposit (current)
            entity.HasOne(s => s.Deposit)
                .WithOne()                                // ✅ không dùng Deposit.Seller
                .HasForeignKey<Seller>(s => s.DepositId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1-n: Seller → Deposits (lịch sử)
            entity.HasMany(s => s.Deposits)
                .WithOne(d => d.Seller)                  // ✅ dùng Deposit.Seller
                .HasForeignKey(d => d.SellerId);
        });

        // VerificationRequests
        modelBuilder.Entity<VerificationRequest>(entity =>
        {
            entity.ToTable("VerificationRequests");
            entity.HasKey(e => e.Id);
            entity.HasOne(v => v.User)
                .WithMany(u => u.VerificationRequests)
                .HasForeignKey(v => v.UserId);
            entity.HasOne(v => v.ReviewedByUser)
                .WithMany()
                .HasForeignKey(v => v.ReviewedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Certificates
        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.ToTable("Certificates");
            entity.HasKey(e => e.Id);
            entity.HasOne(c => c.Seller)
                .WithMany(s => s.Certificates)
                .HasForeignKey(c => c.SellerId);
            entity.HasOne(c => c.Product)
                .WithMany(p => p.Certificates)
                .HasForeignKey(c => c.ProductId);
            entity.HasOne(c => c.VerifiedByUser)
                .WithMany()
                .HasForeignKey(c => c.VerifiedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Deposits
        modelBuilder.Entity<Deposit>(entity =>
        {
            entity.ToTable("Deposits");
            entity.HasKey(e => e.Id);
            // Chỉ định rõ ngược lại cho 1-n
            entity.HasOne(d => d.Seller)
                .WithMany(s => s.Deposits)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReleasedByUser)
                .WithMany()
                .HasForeignKey(d => d.ReleasedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Products ↔ Seller, Category
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(e => e.Id);
            entity.HasOne(p => p.Seller)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SellerId);
            entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId);
        });

        // Categories (đệ quy)
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(e => e.Id);
            entity.HasOne(c => c.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BlindBox ↔ Seller
        modelBuilder.Entity<BlindBox>(entity =>
        {
            entity.ToTable("BlindBoxes");
            entity.HasKey(e => e.Id);
            entity.HasOne(b => b.Seller)
                .WithMany(s => s.BlindBoxes)
                .HasForeignKey(b => b.SellerId);
        });

        // BlindBoxItem ↔ BlindBox, Product
        modelBuilder.Entity<BlindBoxItem>(entity =>
        {
            entity.ToTable("BlindBoxItems");
            entity.HasKey(e => e.Id);
            entity.HasOne(i => i.BlindBox)
                .WithMany(b => b.BlindBoxItems)
                .HasForeignKey(i => i.BlindBoxId);
            entity.HasOne(i => i.Product)
                .WithMany(p => p.BlindBoxItems)
                .HasForeignKey(i => i.ProductId);
        });

        // ProbabilityConfig ↔ BlindBoxItem, ApprovedByUser
        modelBuilder.Entity<ProbabilityConfig>(entity =>
        {
            entity.ToTable("ProbabilityConfigs");
            entity.HasKey(e => e.Id);
            entity.HasOne(pc => pc.BlindBoxItem)
                .WithMany(i => i.ProbabilityConfigs)
                .HasForeignKey(pc => pc.BlindBoxItemId);
            entity.HasOne(pc => pc.ApprovedByUser)
                .WithMany()
                .HasForeignKey(pc => pc.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InventoryItem ↔ Seller, Product
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(e => e.Id);
            entity.HasOne(ii => ii.Seller)
                .WithMany(s => s.InventoryItems)
                .HasForeignKey(ii => ii.SellerId);
            entity.HasOne(ii => ii.Product)
                .WithMany(p => p.InventoryItems)
                .HasForeignKey(ii => ii.ProductId);
        });

        // Cart ↔ User
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.ToTable("Carts");
            entity.HasKey(e => e.Id);
            entity.HasOne(c => c.User)
                .WithMany(u => u.Carts)
                .HasForeignKey(c => c.UserId);
        });

        // CartItem ↔ Cart, Product, BlindBox
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItems");
            entity.HasKey(e => e.Id);
            entity.HasOne(ci => ci.Cart)
                .WithMany(c => c.CartItems)
                .HasForeignKey(ci => ci.CartId);
            entity.HasOne(ci => ci.Product)
                .WithMany(p => p.CartItems)
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(ci => ci.BlindBox)
                .WithMany(b => b.CartItems)
                .HasForeignKey(ci => ci.BlindBoxId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Order ↔ User, Cart, ShippingAddress, Payment
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(e => e.Id);

            entity.HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId);

            entity.HasOne(o => o.Cart)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CartId);

            entity.HasOne(o => o.ShippingAddress)
                .WithMany(a => a.Orders)
                .HasForeignKey(o => o.ShippingAddressId)
                .OnDelete(DeleteBehavior.SetNull);

            // Chuyển mapping với Payment thành 1-1
            entity.HasOne(o => o.Payment)
                .WithOne(p => p.Order)
                .HasForeignKey<Order>(o => o.PaymentId) // FK nằm trên bảng Orders
                .OnDelete(DeleteBehavior.SetNull); // khi Payment bị xóa, Order.PaymentId → null
        });


        // OrderDetail ↔ Order, Product, BlindBox
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.ToTable("OrderDetails");
            entity.HasKey(e => e.Id);
            entity.HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.OrderId);
            entity.HasOne(od => od.Product)
                .WithMany(p => p.OrderDetails)
                .HasForeignKey(od => od.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(od => od.BlindBox)
                .WithMany(b => b.OrderDetails)
                .HasForeignKey(od => od.BlindBoxId)
                .OnDelete(DeleteBehavior.SetNull);
        });

// Payment ↔ Order (1:1)
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.Id);

            entity.HasOne(p => p.Order) // Payment.Order navigation
                .WithOne(o => o.Payment) // Order.Payment navigation
                .HasForeignKey<Payment>(p => p.OrderId) // FK nằm trên bảng Payments
                .OnDelete(DeleteBehavior.Cascade); // hoặc Restrict tuỳ nghiệp vụ
        });


        // Transaction ↔ Payment
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(e => e.Id);
            entity.HasOne(t => t.Payment)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.PaymentId);
        });

        // Address ↔ User
        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Addresses");
            entity.HasKey(e => e.Id);
            entity.HasOne(a => a.User)
                .WithMany(u => u.Addresses)
                .HasForeignKey(a => a.UserId);
        });

        // Shipment ↔ OrderDetail
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("Shipments");
            entity.HasKey(e => e.Id);
            entity.HasOne(s => s.OrderDetail)
                .WithMany(od => od.Shipments)
                .HasForeignKey(s => s.OrderDetailId);
        });

        // Review ↔ User, Product, BlindBox
        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Reviews");
            entity.HasKey(e => e.Id);
            entity.HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId);
            entity.HasOne(r => r.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(r => r.BlindBox)
                .WithMany(b => b.Reviews)
                .HasForeignKey(r => r.BlindBoxId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SupportTicket ↔ User, AssignedTo
        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.ToTable("SupportTickets");
            entity.HasKey(e => e.Id);
            entity.HasOne(t => t.User)
                .WithMany(u => u.SupportTickets)
                .HasForeignKey(t => t.UserId);
            entity.HasOne(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Notification ↔ User
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);
            entity.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId);
        });

        // Promotion (đơn giản)
        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.ToTable("Promotions");
            entity.HasKey(e => e.Id);
        });

        // Wishlist ↔ User
        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.ToTable("Wishlists");
            entity.HasKey(e => e.Id);
            entity.HasOne(w => w.User)
                .WithMany(u => u.Wishlists)
                .HasForeignKey(w => w.UserId);
        });

        // WishlistItem ↔ Wishlist, Product, BlindBox
        modelBuilder.Entity<WishlistItem>(entity =>
        {
            entity.ToTable("WishlistItems");
            entity.HasKey(e => e.Id);
            entity.HasOne(wi => wi.Wishlist)
                .WithMany(w => w.WishlistItems)
                .HasForeignKey(wi => wi.WishlistId);
            entity.HasOne(wi => wi.Product)
                .WithMany(p => p.WishlistItems)
                .HasForeignKey(wi => wi.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(wi => wi.BlindBox)
                .WithMany(b => b.WishlistItems)
                .HasForeignKey(wi => wi.BlindBoxId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}