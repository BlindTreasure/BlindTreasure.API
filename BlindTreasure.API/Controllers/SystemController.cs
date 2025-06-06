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
            await SeedProducts();
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

        // var sneaker = new Category
        // {
        //     Id = Guid.NewGuid(),
        //     Name = "Sneaker",
        //     Description = "Danh mục giày sneaker thời trang.",
        //     CreatedAt = now
        // };

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
            // new()
            // {
            //     Name = "Smiski",
            //     Description = "Đồ chơi sưu tầm nhân vật Smiski phát sáng.",
            //     ParentId = collectibleToys.Id,
            //     CreatedAt = now
            // },
            new()
            {
                Name = "Baby Three",
                Description = "Mẫu đồ chơi sưu tầm dòng Baby Three.",
                ParentId = collectibleToys.Id,
                CreatedAt = now
            },
            // new()
            // {
            //     Name = "Marvel",
            //     Description = "Mô hình nhân vật vũ trụ Marvel.",
            //     ParentId = collectibleToys.Id,
            //     CreatedAt = now
            // },
            // new()
            // {
            //     Name = "Gundam",
            //     Description = "Mô hình robot lắp ráp dòng Gundam.",
            //     ParentId = collectibleToys.Id,
            //     CreatedAt = now
            // },
            // new()
            // {
            //     Name = "Adidas",
            //     Description = "Giày sneaker thương hiệu Adidas.",
            //     ParentId = sneaker.Id,
            //     CreatedAt = now
            // },
            // new()
            // {
            //     Name = "Nike",
            //     Description = "Giày sneaker thương hiệu Nike.",
            //     ParentId = sneaker.Id,
            //     CreatedAt = now
            // }
        };

        // Thêm vào context
        await _context.Categories.AddRangeAsync(collectibleToys);
        await _context.Categories.AddRangeAsync(children);
        await _context.SaveChangesAsync();

        await _cacheService.RemoveByPatternAsync("category:all");

        _logger.Success("[SeedCategories] Seed danh mục thành công.");
    }

    private async Task SeedProducts()
    {
        var sellerUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "trangiaphuc362003181@gmail.com");
        if (sellerUser == null)
        {
            _logger.Error("Không tìm thấy user Seller với email trangiaphuc362003181@gmail.com để tạo product.");
            return;
        }

        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == sellerUser.Id);
        if (seller == null)
        {
            _logger.Error("User này chưa có Seller tương ứng.");
            return;
        }

        var categories = await _context.Categories.Where(c => !c.IsDeleted && c.ParentId != null).ToListAsync();
        // Chỉ seed sản phẩm cho category con (có ParentId) để rõ ràng

        var now = DateTime.UtcNow;
        var products = new List<Product>();

        foreach (var category in categories)
            switch (category.Name)
            {
                case "PopMart":
                    products.AddRange(new[]
                    {
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Baby Molly Funny Raining Day Figure",
                            Description = "Đồ chơi sưu tầm PopMart phiên bản 1, thiết kế độc đáo.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 350000,
                            Stock = 50,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string> { "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FBaby%20Molly%20Funny%20Raining%20Day%20Figure.jpg&version_id=null" },
                            Brand = "PopMart",
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 8
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Hand in Hand Series Figures\n",
                            Description = "Mô hình PopMart phiên bản giới hạn.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 450000,
                            Stock = 30,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string> { "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FHand%20in%20Hand%20Series%20Figures.jpg&version_id=null" },
                            Brand = "PopMart",
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 10
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "CHAKA Candle Whisper Series Figures",
                            Description = "Blind box PopMart kích thước nhỏ gọn, thích hợp sưu tầm.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 200000,
                            Stock = 70,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string> { "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FCHAKA%20Candle%20Whisper%20Series%20Figures.jpg&version_id=null" },
                            Brand = "PopMart",
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 6
                        }
                    });
                    break;

                case "Baby Three":
                    products.AddRange(new[]
                    {
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Porker",
                            Description = "Đồ chơi Baby Three phiên bản A, chất liệu an toàn.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 400000,
                            Stock = 40,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string> { "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2Fporker.webp&version_id=null" },
                            Brand = "Baby Three",
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 9
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Rabbit",
                            Description = "Búp bê Baby Three bằng vải mềm mại, thân thiện với trẻ em.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 550000,
                            Stock = 25,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string> { "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2Frabbit.webp&version_id=null" },
                            Brand = "Baby Three",
                            Material = "Fabric",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 15
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Búp Bê Baby Three V3 Vinyl Plush Dinosaur Màu Xanh Lá\n",
                            Description = "Phiên bản sưu tầm Baby Three giới hạn số lượng.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 700000,
                            Stock = 10,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string> { "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FB%C3%BAp%20B%C3%AA%20Baby%20Three%20V3%20Vinyl%20Plush%20Dinosaur%20M%C3%A0u%20Xanh%20L%C3%A1.webp&version_id=null" },
                            Brand = "Baby Three",
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 11
                        }
                    });
                    break;

                // case "Marvel":
                //     products.AddRange(new[]
                //     {
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Marvel Iron Man Figure",
                //             Description = "Mô hình Iron Man chi tiết cao, thích hợp sưu tập.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 1200000,
                //             Stock = 15,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/marvel1.jpg" },
                //             Brand = "Marvel",
                //             Material = "PVC",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = 18
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Marvel Spider-Man Statue",
                //             Description = "Tượng Spider-Man phiên bản đặc biệt, giới hạn.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 1100000,
                //             Stock = 20,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/marvel2.jpg" },
                //             Brand = "Marvel",
                //             Material = "Resin",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = 20
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Marvel Captain America Figure",
                //             Description = "Mô hình Captain America, hàng chính hãng Marvel.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 1300000,
                //             Stock = 10,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/marvel3.jpg" },
                //             Brand = "Marvel",
                //             Material = "PVC",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = 17
                //         }
                //     });
                //     break;
                //
                // case "Gundam":
                //     products.AddRange(new[]
                //     {
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Gundam RX-78-2 Model",
                //             Description = "Mô hình Gundam RX-78-2 chi tiết, lắp ráp được.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 900000,
                //             Stock = 30,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/gundam1.jpg" },
                //             Brand = "Bandai",
                //             Material = "Plastic",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = 25
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Gundam Wing Zero Custom",
                //             Description = "Mô hình Gundam Wing Zero phiên bản tùy chỉnh.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 1200000,
                //             Stock = 20,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/gundam2.jpg" },
                //             Brand = "Bandai",
                //             Material = "Plastic",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = 28
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Gundam Strike Rouge",
                //             Description = "Mô hình Gundam Strike Rouge chính hãng.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 1000000,
                //             Stock = 15,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/gundam3.jpg" },
                //             Brand = "Bandai",
                //             Material = "Plastic",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = 27
                //         }
                //     });
                //     break;
                //
                // case "Adidas":
                //     products.AddRange(new[]
                //     {
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Adidas Ultraboost 22",
                //             Description = "Giày Adidas Ultraboost phiên bản 2022, thoáng khí và êm ái.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 4500000,
                //             Stock = 40,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/adidas1.jpg" },
                //             Brand = "Adidas",
                //             Material = "Synthetic",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = null
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Adidas Superstar",
                //             Description = "Giày Adidas Superstar cổ điển, phong cách thời trang.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 3200000,
                //             Stock = 30,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/adidas2.jpg" },
                //             Brand = "Adidas",
                //             Material = "Leather",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = null
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Adidas NMD R1",
                //             Description = "Giày Adidas NMD R1 với thiết kế hiện đại và thoải mái.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 4000000,
                //             Stock = 20,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/adidas3.jpg" },
                //             Brand = "Adidas",
                //             Material = "Synthetic",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = null
                //         }
                //     });
                //     break;
                //
                // case "Nike":
                //     products.AddRange(new[]
                //     {
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Nike Air Max 270",
                //             Description = "Giày Nike Air Max 270 phiên bản thể thao, êm ái.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 5000000,
                //             Stock = 35,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/nike1.jpg" },
                //             Brand = "Nike",
                //             Material = "Synthetic",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = null
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Nike Dunk Low",
                //             Description = "Giày Nike Dunk Low phong cách cổ điển.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 3500000,
                //             Stock = 25,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/nike2.jpg" },
                //             Brand = "Nike",
                //             Material = "Leather",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = null
                //         },
                //         new Product
                //         {
                //             Id = Guid.NewGuid(),
                //             Name = "Nike React Infinity Run",
                //             Description = "Giày Nike React với công nghệ giảm chấn tối ưu.",
                //             CategoryId = category.Id,
                //             SellerId = seller.Id,
                //             Price = 4800000,
                //             Stock = 20,
                //             Status = ProductStatus.Active,
                //             CreatedAt = now,
                //             ImageUrls = new List<string> { "https://example.com/nike3.jpg" },
                //             Brand = "Nike",
                //             Material = "Synthetic",
                //             ProductType = ProductSaleType.DirectSale,
                //             Height = null
                //         }
                //     });
                //     break;

                default:
                    _logger.Warn($"Chưa có dữ liệu mẫu cho category {category.Name}, bỏ qua tạo sản phẩm.");
                    break;
            }

        if (products.Count > 0)
        {
            await _context.Products.AddRangeAsync(products);
            await _context.SaveChangesAsync();
            _logger.Success("[SeedProducts] Seed sản phẩm chuẩn thành công.");
        }
        else
        {
            _logger.Warn("[SeedProducts] Không có sản phẩm nào được tạo do không có category con phù hợp.");
        }
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
                await _cacheService.RemoveByPatternAsync("seller:");
                await _cacheService.RemoveByPatternAsync("product:");

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
                CreatedAt = now
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