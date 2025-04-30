using BlindTreasure.Domain;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly BlindTreasureDbContext _dbContext;

    public UnitOfWork()
    {
        
        
    }
    
    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}