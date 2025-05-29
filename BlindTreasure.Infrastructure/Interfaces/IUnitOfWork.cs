using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Infrastructure.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<User> Users { get; }
    IGenericRepository<Seller> Sellers { get; }
    IGenericRepository<OtpVerification> OtpVerifications { get; }
    IGenericRepository<Category> Categories { get; }

    Task<int> SaveChangesAsync();
}