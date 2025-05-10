using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Infrastructure.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<User> Users { get; }

    Task<int> SaveChangesAsync();
}