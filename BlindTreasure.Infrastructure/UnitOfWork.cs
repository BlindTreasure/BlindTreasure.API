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
    IGenericRepository<ProbabilityConfig> probabilityConfigs)
    : IUnitOfWork
{
    public IGenericRepository<User> Users { get; } = userRepository;
    public IGenericRepository<Seller> Sellers { get; } = sellers;
    public IGenericRepository<OtpVerification> OtpVerifications { get; } = otpVerifications;
    public IGenericRepository<Category> Categories { get; } = categories;
    public IGenericRepository<Product> Products { get; } = products;
    public IGenericRepository<BlindBox> BlindBoxes { get; } = blindBoxes;
    public IGenericRepository<BlindBoxItem> BlindBoxItems { get; } = blindBoxItems;
    public IGenericRepository<ProbabilityConfig> ProbabilityConfigs { get; } = probabilityConfigs;

    public void Dispose()
    {
        dbContext.Dispose();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await dbContext.SaveChangesAsync();
    }
}