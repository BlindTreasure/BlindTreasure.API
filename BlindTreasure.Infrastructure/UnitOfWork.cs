using BlindTreasure.Domain;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlindTreasure.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private IDbContextTransaction? _transaction;
    private readonly BlindTreasureDbContext _dbContext;

    public UnitOfWork(
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
        IGenericRepository<Shipment> shipments,
        IGenericRepository<ListingReport> listingReports,
        IGenericRepository<TradeHistory> tradeHistories,
        IGenericRepository<TradeRequest> tradeRequests,
        IGenericRepository<TradeRequestItem> tradeRequestItems,
        IGenericRepository<CustomerFavourite> customerFavourites,
        IGenericRepository<OrderSellerPromotion> orderSellerPromotion, IGenericRepository<Review> reviews, IDbContextTransaction? transaction = null
    )
    {
        _dbContext = dbContext;
        _transaction = transaction;
        Users = userRepository;
        Sellers = sellers;
        OtpVerifications = otpVerifications;
        Categories = categories;
        Products = products;
        BlindBoxes = blindBoxes;
        BlindBoxItems = blindBoxItems;
        RarityConfigs = rarityConfigs;
        ProbabilityConfigs = probabilityConfigs;
        Promotions = promotions;
        CartItems = cartItems;
        Orders = orders;
        OrderDetails = orderDetails;
        Transactions = transactions;
        Payments = payments;
        Addresses = addresses;
        InventoryItems = inventoryItems;
        CustomerBlindBoxes = customerBlindboxes;
        CustomerFavourites = customerFavourites;
        Notifications = notifications;
        PromotionParticipants = promotionParticipants;
        Listings = listings;
        ChatMessages = chatMessages;
        BlindBoxUnboxLogs = blindBoxUnboxLogs;
        Shipments = shipments;
        ListingReports = listingReports;
        TradeHistories = tradeHistories;
        TradeRequests = tradeRequests;
        TradeRequestItems = tradeRequestItems;
        OrderSellerPromotions = orderSellerPromotion;
        Reviews = reviews;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _dbContext.SaveChangesAsync();
    }

    // Transaction support
    public async Task BeginTransactionAsync()
    {
        if (_transaction != null) return;
        _transaction = await _dbContext.Database.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        try
        {
            if (_transaction != null)
            {
                await _dbContext.SaveChangesAsync();
                await _transaction.CommitAsync();
            }
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackAsync()
    {
        try
        {
            if (_transaction != null)
                await _transaction.RollbackAsync();
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public IGenericRepository<User> Users { get; }
    public IGenericRepository<Seller> Sellers { get; }
    public IGenericRepository<OtpVerification> OtpVerifications { get; }
    public IGenericRepository<Category> Categories { get; }
    public IGenericRepository<Product> Products { get; }
    public IGenericRepository<BlindBox> BlindBoxes { get; }
    public IGenericRepository<BlindBoxItem> BlindBoxItems { get; }
    public IGenericRepository<RarityConfig> RarityConfigs { get; }
    public IGenericRepository<ProbabilityConfig> ProbabilityConfigs { get; }
    public IGenericRepository<Promotion> Promotions { get; }
    public IGenericRepository<CartItem> CartItems { get; }
    public IGenericRepository<Order> Orders { get; }
    public IGenericRepository<OrderDetail> OrderDetails { get; }
    public IGenericRepository<Transaction> Transactions { get; }
    public IGenericRepository<Payment> Payments { get; }
    public IGenericRepository<Address> Addresses { get; }
    public IGenericRepository<InventoryItem> InventoryItems { get; }
    public IGenericRepository<CustomerBlindBox> CustomerBlindBoxes { get; }
    public IGenericRepository<CustomerFavourite> CustomerFavourites { get; }
    public IGenericRepository<Notification> Notifications { get; }
    public IGenericRepository<PromotionParticipant> PromotionParticipants { get; }
    public IGenericRepository<Listing> Listings { get; }
    public IGenericRepository<ChatMessage> ChatMessages { get; }
    public IGenericRepository<BlindBoxUnboxLog> BlindBoxUnboxLogs { get; }
    public IGenericRepository<Shipment> Shipments { get; }
    public IGenericRepository<ListingReport> ListingReports { get; }
    public IGenericRepository<TradeHistory> TradeHistories { get; }
    public IGenericRepository<TradeRequest> TradeRequests { get; }
    public IGenericRepository<TradeRequestItem> TradeRequestItems { get; }
    public IGenericRepository<OrderSellerPromotion> OrderSellerPromotions { get; }
    public IGenericRepository<Review> Reviews { get; }
}