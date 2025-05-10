using BlindTreasure.Domain;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using BlindTreasure.Infrastructure.Repositories;

namespace BlindTreasure.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly BlindTreasureDbContext _dbContext;
    
    public UnitOfWork(BlindTreasureDbContext dbContext,
        IGenericRepository<User> userRepository)
    {
        _dbContext = dbContext;
        Users = userRepository;
    }

    public IGenericRepository<User> Users { get; private set; }
    
    public void Dispose()
    {
        _dbContext.Dispose();
    }
    
    public async Task<int> SaveChangesAsync()
    {
        return await _dbContext.SaveChangesAsync();
    }
}