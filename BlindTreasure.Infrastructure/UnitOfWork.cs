using BlindTreasure.Domain;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Infrastructure;

public class UnitOfWork(
    BlindTreasureDbContext dbContext,
    IGenericRepository<User> userRepository,
    IGenericRepository<OtpVerification> otpVerifications,
    IGenericRepository<Seller> sellers,
    IGenericRepository<Category> categories,
    IGenericRepository<Product> products,
    IGenericRepository<BlindBox> blindBoxes,
    IGenericRepository<BlindBoxItem> blindBoxItems,
    IGenericRepository<ProbabilityConfig> probabilityConfigs,
    IGenericRepository<Promotion> promotions,
    IGenericRepository<CartItem> cartItems,
    IGenericRepository<Order> orders,
    IGenericRepository<OrderDetail> orderDetails,
    IGenericRepository<Transaction> transactions,
    IGenericRepository<Payment> payments,
    IGenericRepository<Address> addresses,
    IGenericRepository<InventoryItem> inventoryItems,
    IGenericRepository<CustomerBlindBox> customerBlindboxes,
    IGenericRepository<Notification> notifications,
    IGenericRepository<RarityConfig> rarityConfigs,
    IGenericRepository<PromotionParticipant> promotionParticipants,
    IGenericRepository<Listing> listings,
    IGenericRepository<ChatMessage> chatMessages,
    IGenericRepository<BlindBoxUnboxLog> blindBoxUnboxLogs,
    IGenericRepository<Shipment> Shipments,
    IGenericRepository<ListingReport> listingReports,
    IGenericRepository<TradeHistory> tradeHistories,
    IGenericRepository<TradeRequest> tradeRequests,
    IGenericRepository<TradeRequestItem> tradeRequestItems,
    IGenericRepository<CustomerFavourite> customerFavourites,
    IGenericRepository<OrderSellerPromotion> orderSellerPromotion,
    IGenericRepository<Review> reviews)
    : IUnitOfWork
{
    public IGenericRepository<User> Users { get; } = userRepository;
    public IGenericRepository<Seller> Sellers { get; } = sellers;
    public IGenericRepository<OtpVerification> OtpVerifications { get; } = otpVerifications;
    public IGenericRepository<Category> Categories { get; } = categories;
    public IGenericRepository<Product> Products { get; } = products;
    public IGenericRepository<BlindBox> BlindBoxes { get; } = blindBoxes;
    public IGenericRepository<BlindBoxItem> BlindBoxItems { get; } = blindBoxItems;
    public IGenericRepository<RarityConfig> RarityConfigs { get; } = rarityConfigs;
    public IGenericRepository<ProbabilityConfig> ProbabilityConfigs { get; } = probabilityConfigs;
    public IGenericRepository<Promotion> Promotions { get; } = promotions;
    public IGenericRepository<CartItem> CartItems { get; } = cartItems;
    public IGenericRepository<Order> Orders { get; } = orders;
    public IGenericRepository<OrderDetail> OrderDetails { get; } = orderDetails;
    public IGenericRepository<Transaction> Transactions { get; } = transactions;
    public IGenericRepository<Payment> Payments { get; } = payments;
    public IGenericRepository<Address> Addresses { get; } = addresses;
    public IGenericRepository<InventoryItem> InventoryItems { get; } = inventoryItems;
    public IGenericRepository<CustomerBlindBox> CustomerBlindBoxes { get; } = customerBlindboxes;
    public IGenericRepository<CustomerFavourite> CustomerFavourites { get; } = customerFavourites;
    public IGenericRepository<Notification> Notifications { get; } = notifications;
    public IGenericRepository<PromotionParticipant> PromotionParticipants { get; } = promotionParticipants;
    public IGenericRepository<Listing> Listings { get; } = listings;
    public IGenericRepository<ChatMessage> ChatMessages { get; } = chatMessages;
    public IGenericRepository<BlindBoxUnboxLog> BlindBoxUnboxLogs { get; } = blindBoxUnboxLogs;
    public IGenericRepository<Shipment> Shipments { get; } = Shipments;
    public IGenericRepository<ListingReport> ListingReports { get; } = listingReports;
    public IGenericRepository<TradeHistory> TradeHistories { get; } = tradeHistories;
    public IGenericRepository<TradeRequest> TradeRequests { get; } = tradeRequests;
    public IGenericRepository<TradeRequestItem> TradeRequestItems { get; } = tradeRequestItems;
    public IGenericRepository<OrderSellerPromotion> OrderSellerPromotions { get; } = orderSellerPromotion;
    public IGenericRepository<Review> Reviews { get; } = reviews;

    public void Dispose()
    {
        dbContext.Dispose();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await dbContext.SaveChangesAsync();
    }
}