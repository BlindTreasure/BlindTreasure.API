using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly BlindTreasureDbContext _context;
    private readonly ILoggerService _logger;

    public SystemController(BlindTreasureDbContext context, ILoggerService logger, ICacheService cacheService)
    {
        _context = context;
        _logger = logger;
        _cacheService = cacheService;
    }

    [HttpPost("seed-all-data")]
    public async Task<IActionResult> SeedData()
    {
        try
        {
            await ClearDatabase(_context);

            // Seed data
            await SeedRolesAndUsers();
            return Ok(ApiResult<object>.Success(new
            {
                Message = "Data seeded successfully."
            }));
        }
        catch (DbUpdateException dbEx)
        {
            _logger.Error($"Database update error: {dbEx.Message}");
            return StatusCode(500, "Error seeding data: Database issue.");
        }
        catch (Exception ex)
        {
            _logger.Error($"General error: {ex.Message}");
            return StatusCode(500, "Error seeding data: General failure.");
        }
    }

    private async Task SeedRolesAndUsers()
    {
        // Seed Roles
        var roles = new List<Role>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Type = RoleType.Seller,
                Description = "Người bán chính thức"
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Type = RoleType.Customer,
                Description = "Khách hàng"
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Type = RoleType.Staff,
                Description = "Nhân viên"
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Type = RoleType.Admin,
                Description = "Quản trị hệ thống"
            }
        };

        _logger.Info("Seeding roles...");
        await _context.Roles.AddRangeAsync(roles);
        await _context.SaveChangesAsync();
        _logger.Success("Roles seeded successfully.");

        // Seed Users
        var passwordHasher = new PasswordHasher();
        var now = DateTime.UtcNow;
        var defaultExpire = now.AddDays(1);

        var users = new List<User>
        {
            new()
            {
                Email = "seller@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Seller User",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller
            },
            new()
            {
                Email = "a@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Customer User",
                Phone = "0900000002",
                Status = UserStatus.Active,
                RoleName = RoleType.Customer
            },
            new()
            {
                Email = "staff@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Staff User",
                Phone = "0900000003",
                Status = UserStatus.Active,
                RoleName = RoleType.Staff
            },
            new()
            {
                Email = "admin@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Admin User",
                Phone = "0900000004",
                Status = UserStatus.Active,
                RoleName = RoleType.Admin
            }
        };

        _logger.Info("Seeding users...");
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        _logger.Success("Users seeded successfully.");
    }


    private async Task ClearDatabase(BlindTreasureDbContext context)
    {
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            _logger.Info("Bắt đầu xóa dữ liệu trong database...");

            var tablesToDelete = new List<Func<Task>>
            {
                // Bảng phụ thuộc trước
                () => context.ProbabilityConfigs.ExecuteDeleteAsync(),
                () => context.BlindBoxItems.ExecuteDeleteAsync(),
                () => context.CartItems.ExecuteDeleteAsync(),
                () => context.OrderDetails.ExecuteDeleteAsync(),
                () => context.Listings.ExecuteDeleteAsync(),
                () => context.InventoryItems.ExecuteDeleteAsync(),
                () => context.WishlistItems.ExecuteDeleteAsync(),
                () => context.SupportTickets.ExecuteDeleteAsync(),
                () => context.Reviews.ExecuteDeleteAsync(),
                () => context.Shipments.ExecuteDeleteAsync(),
                () => context.Transactions.ExecuteDeleteAsync(),
                () => context.Notifications.ExecuteDeleteAsync(),
                () => context.OtpVerifications.ExecuteDeleteAsync(),

                // Bảng chính
                () => context.Wishlists.ExecuteDeleteAsync(),
                () => context.CustomerDiscounts.ExecuteDeleteAsync(),
                () => context.Orders.ExecuteDeleteAsync(),
                () => context.Payments.ExecuteDeleteAsync(),
                () => context.Promotions.ExecuteDeleteAsync(),
                () => context.Addresses.ExecuteDeleteAsync(),
                () => context.Products.ExecuteDeleteAsync(),
                () => context.BlindBoxes.ExecuteDeleteAsync(),
                () => context.Certificates.ExecuteDeleteAsync(),
                () => context.Categories.ExecuteDeleteAsync(),
                () => context.Sellers.ExecuteDeleteAsync(),
                () => context.Users.ExecuteDeleteAsync(),
                () => context.Roles.ExecuteDeleteAsync()
            };

            foreach (var deleteFunc in tablesToDelete) await deleteFunc();
            await transaction.CommitAsync();

            await _cacheService.RemoveByPatternAsync("user:");

            _logger.Success("Xóa sạch dữ liệu trong database thành công.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.Error($"Xóa dữ liệu thất bại: {ex.Message}");
            throw;
        }
    }
}