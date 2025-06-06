﻿using BlindTreasure.Domain.Entities;

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

    Task<int> SaveChangesAsync();
}