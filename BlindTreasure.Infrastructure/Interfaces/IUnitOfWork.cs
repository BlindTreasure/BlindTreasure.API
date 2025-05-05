namespace BlindTreasure.Infrastructure.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();
}