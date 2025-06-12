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
    IGenericRepository<ProbabilityConfig> ProbabilityConfigs { get; }
    IGenericRepository<Order> Orders { get; }
    IGenericRepository<CartItem> CartItems { get; }
    IGenericRepository<OrderDetail> OrderDetails { get; }
    IGenericRepository<Transaction> Transactions { get; }
    IGenericRepository<Payment> Payments { get; }

    Task<int> SaveChangesAsync();
}