using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Domain;

public class BlindTreasureDbContext : DbContext
{
    public BlindTreasureDbContext()
    {
    }

    public BlindTreasureDbContext(DbContextOptions<BlindTreasureDbContext> options)
        : base(options)
    {
    }
}