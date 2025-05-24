using BlindTreasure.Domain;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly BlindTreasureDbContext _dbContext;

    public UnitOfWork(BlindTreasureDbContext dbContext,
        IGenericRepository<User> userRepository,
        IGenericRepository<OtpVerification> otpVerifications, IGenericRepository<Seller> sellers)
    {
        _dbContext = dbContext;
        Users = userRepository;
        OtpVerifications = otpVerifications;
        Sellers = sellers;
    }

    public IGenericRepository<User> Users { get; }
    public IGenericRepository<Seller> Sellers { get; }
    public IGenericRepository<OtpVerification> OtpVerifications { get; }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _dbContext.SaveChangesAsync();
    }
}