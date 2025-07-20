using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Infrastructure.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<User> Users { get; }
    IGenericRepository<Seller> Sellers { get; }
    IGenericRepository<OtpVerification> OtpVerifications { get; }
    IGenericRepository<Category> Categories { get; }
    IGenericRepository<Product> Products { get; }
    IGenericRepository<BlindBox> BlindBoxes { get; }
    IGenericRepository<BlindBoxItem> BlindBoxItems { get; }
    IGenericRepository<RarityConfig> RarityConfigs { get; }
    IGenericRepository<ProbabilityConfig> ProbabilityConfigs { get; }
    IGenericRepository<Promotion> Promotions { get; }
    IGenericRepository<PromotionParticipant> PromotionParticipants { get; }
    IGenericRepository<Order> Orders { get; }
    IGenericRepository<CartItem> CartItems { get; }
    IGenericRepository<OrderDetail> OrderDetails { get; }
    IGenericRepository<Transaction> Transactions { get; }
    IGenericRepository<Payment> Payments { get; }
    IGenericRepository<Address> Addresses { get; }
    IGenericRepository<InventoryItem> InventoryItems { get; }
    IGenericRepository<CustomerBlindBox> CustomerBlindBoxes { get; }
    IGenericRepository<Notification> Notifications { get; }
    IGenericRepository<Listing> Listings { get; }
    IGenericRepository<ChatMessage> ChatMessages { get; }
    IGenericRepository<BlindBoxUnboxLog> BlindBoxUnboxLogs { get; }
    IGenericRepository<Shipment> Shipments { get; }
    IGenericRepository<ListingReport> ListingReports { get; }
    IGenericRepository<TradeRequest> TradeRequests { get; }
    IGenericRepository<TradeHistory> TradeHistories { get; }

    Task<int> SaveChangesAsync();
}