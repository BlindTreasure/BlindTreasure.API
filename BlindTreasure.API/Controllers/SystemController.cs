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
            await SeedCategories();
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
        await SeedRoles();

        var users = GetPredefinedUsers();
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        await SeedSellerForUser("trangiaphuc362003181@gmail.com");

        _logger.Success("Users and seller seeded successfully.");
    }

    private async Task SeedCategories()
    {
        if (_context.Categories.Any())
        {
            _logger.Info("[SeedCategories] Đã tồn tại danh mục. Bỏ qua seed.");
            return;
        }

        var now = DateTime.UtcNow;

        // Danh sách category cha
        var collectibleToys = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Collectible Toys",
            Description = "Danh mục đồ chơi sưu tầm, thiết kế đặc biệt và giới hạn.",
            CreatedAt = now
        };

        var sneaker = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Sneaker",
            Description = "Danh mục giày sneaker thời trang.",
            CreatedAt = now
        };

        // Danh sách category con
        var children = new List<Category>
        {
            new()
            {
                Name = "PopMart",
                Description = "Đồ chơi sưu tầm thương hiệu PopMart.",
                ParentId = collectibleToys.Id,
                CreatedAt = now
            },
            new()
            {
                Name = "Smiski",
                Description = "Đồ chơi sưu tầm nhân vật Smiski phát sáng.",
                ParentId = collectibleToys.Id,
                CreatedAt = now
            },
            new()
            {
                Name = "Baby Three",
                Description = "Mẫu đồ chơi sưu tầm dòng Baby Three.",
                ParentId = collectibleToys.Id,
                CreatedAt = now
            },
            new()
            {
                Name = "Marvel",
                Description = "Mô hình nhân vật vũ trụ Marvel.",
                ParentId = collectibleToys.Id,
                CreatedAt = now
            },
            new()
            {
                Name = "Gundam",
                Description = "Mô hình robot lắp ráp dòng Gundam.",
                ParentId = collectibleToys.Id,
                CreatedAt = now
            },
            new()
            {
                Name = "Adidas",
                Description = "Giày sneaker thương hiệu Adidas.",
                ParentId = sneaker.Id,
                CreatedAt = now
            },
            new()
            {
                Name = "Nike",
                Description = "Giày sneaker thương hiệu Nike.",
                ParentId = sneaker.Id,
                CreatedAt = now
            }
        };

        // Thêm vào context
        await _context.Categories.AddRangeAsync(collectibleToys, sneaker);
        await _context.Categories.AddRangeAsync(children);
        await _context.SaveChangesAsync();

        await _cacheService.RemoveByPatternAsync("category:all");

        _logger.Success("[SeedCategories] Seed danh mục thành công.");
    }

    private async Task ClearDatabase(BlindTreasureDbContext context)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                _logger.Info("Bắt đầu xóa dữ liệu trong database...");

                var tablesToDelete = new List<Func<Task>>
                {
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
        });
    }

    #region private methods

    private async Task SeedRoles()
    {
        var roles = new List<Role>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Type = RoleType.Seller,
                Description = "Người bán chính thức"
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Type = RoleType.Customer,
                Description = "Khách hàng"
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Type = RoleType.Staff,
                Description = "Nhân viên"
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Type = RoleType.Admin,
                Description = "Quản trị hệ thống"
            }
        };

        _logger.Info("Seeding roles...");
        await _context.Roles.AddRangeAsync(roles);
        await _context.SaveChangesAsync();
        _logger.Success("Roles seeded successfully.");
    }

    private List<User> GetPredefinedUsers()
    {
        var passwordHasher = new PasswordHasher();
        var now = DateTime.UtcNow;

        return new List<User>
        {
            new()
            {
                Email = "blindtreasurefpt@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Customer User",
                Phone = "0900000002",
                Status = UserStatus.Active,
                RoleName = RoleType.Customer,
                CreatedAt = now,
            },
            new()
            {
                Email = "staff@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Staff User",
                Phone = "0900000003",
                Status = UserStatus.Active,
                RoleName = RoleType.Staff,
                CreatedAt = now
            },
            new()
            {
                Email = "admin@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Admin User",
                Phone = "0900000004",
                Status = UserStatus.Active,
                RoleName = RoleType.Admin,
                CreatedAt = now
            },
            new()
            {
                Email = "trangiaphuc362003181@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Seller User",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller,
                CreatedAt = now
            }
        };
    }

    private async Task SeedSellerForUser(string sellerEmail)
    {
        var sellerUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == sellerEmail);
        if (sellerUser == null)
        {
            _logger.Error($"Không tìm thấy user với email {sellerEmail} để tạo Seller.");
            return;
        }

        var seller = new Seller
        {
            UserId = sellerUser.Id,
            IsVerified = true,
            CoaDocumentUrl = "https://example.com/coa.pdf",
            CompanyName = "Blind Treasure Ltd.",
            TaxId = "987654321",
            CompanyAddress = "District 1, HCMC",
            Status = SellerStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Sellers.AddAsync(seller);
        await _context.SaveChangesAsync();
        _logger.Info("Seller seeded successfully.");
    }

    #endregion
}