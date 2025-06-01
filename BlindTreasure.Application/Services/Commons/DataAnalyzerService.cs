using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services.Commons;

public class DataAnalyzerService : IDataAnalyzerService
{
    private readonly IUnitOfWork _unitOfWork;

    public DataAnalyzerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    
    public async Task<List<UserDto>> GetUsersForAiAnalysisAsync()
    {
        var users = await _unitOfWork.Users.GetQueryable()
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Take(20) 
            .ToListAsync();
        var userDtos = users.Select(UserMapper.ToUserDto).ToList();

        return userDtos;
    }
    public async Task<List<Product>> GetProductsAiAnalysisAsync()
    {
        var products = await _unitOfWork.Products.GetQueryable()
            .Where(p => !p.IsDeleted)
            .Include(p => p.Seller)
            .Include(p => p.Category)
            .Include(p => p.Certificates)
            .Include(p => p.InventoryItems)
            .Include(p => p.Reviews)
            .ToListAsync();

        return products;
    }
}