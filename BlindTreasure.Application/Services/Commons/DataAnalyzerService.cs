using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services.Commons;

public class DataAnalyzerService : IDataAnalyzerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderService _orderService;

    public DataAnalyzerService(IUnitOfWork unitOfWork, IOrderService orderService)
    {
        _unitOfWork = unitOfWork;
        _orderService = orderService;
    }

    public async Task<List<OrderDto>> GetMyOrdersForAiAsync(int limit = 5)
    {
        var param = new OrderQueryParameter
        {
            PageIndex = 1,
            PageSize = limit,
            Desc = true
        };

        // tái sử dụng OrderService để lấy theo currentUserId
        var orders = await _orderService.GetMyOrdersAsync(param);
        return orders;
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

    public async Task<List<ProductTrendingStatDto>> GetTrendingProductsForAiAsync()
    {
        var now = DateTime.UtcNow;
        var last30Days = now.AddDays(-30);
        var prev30Days = now.AddDays(-60);

        var query = await _unitOfWork.OrderDetails.GetQueryable()
            .Where(od => !od.IsDeleted
                         && od.ProductId != null
                         && od.Order != null
                         && od.Order.CompletedAt != null)
            .Include(od => od.Product)
            .ThenInclude(p => p.Reviews)
            .Include(od => od.Product)
            .ThenInclude(p => p.CustomerFavourites)
            .ToListAsync();

        var grouped = query
            .GroupBy(od => od.Product!)
            .Select(g =>
            {
                var product = g.Key;

                var recent = g.Where(x => x.Order.CompletedAt >= last30Days).ToList();
                var previous = g.Where(x => x.Order.CompletedAt < last30Days && x.Order.CompletedAt >= prev30Days)
                    .ToList();

                var recentQty = recent.Sum(x => x.Quantity);
                var previousQty = previous.Sum(x => x.Quantity);

                double growthRate = 0;
                if (previousQty > 0)
                    growthRate = (double)(recentQty - previousQty) / previousQty * 100;

                return new ProductTrendingStatDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    TotalOrders = g.Count(),
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.TotalPrice),
                    ReviewCount = product.Reviews?.Count ?? 0,
                    FavouriteCount = product.CustomerFavourites?.Count ?? 0,
                    GrowthRate = growthRate
                };
            })
            .OrderByDescending(x => x.TotalQuantity)
            .Take(50) // lấy top 50 để đưa cho AI phân tích
            .ToList();
        return grouped;
    }
}