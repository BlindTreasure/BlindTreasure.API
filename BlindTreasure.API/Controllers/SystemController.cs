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
            await SeedBlindBoxes();
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

    [HttpDelete("clear-caching")]
    public async Task<IActionResult> ClearCaching()
    {
        try
        {
            await _cacheService.RemoveByPatternAsync("user:");
            await _cacheService.RemoveByPatternAsync("seller:");
            await _cacheService.RemoveByPatternAsync("product:");
            await _cacheService.RemoveByPatternAsync("category:");
            await _cacheService.RemoveByPatternAsync("blindbox:");
            await _cacheService.RemoveByPatternAsync("gemini:");
            await _cacheService.RemoveByPatternAsync("address:");

            return Ok(ApiResult<object>.Success("200", "Clear caching thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
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
                await _cacheService.RemoveByPatternAsync("category:");

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

    #region data seeding

    private async Task SeedRolesAndUsers()
    {
        await SeedRoles();

        var users = GetPredefinedUsers();
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        await SeedSellerForUser("blindtreasurefpt@gmail.com");

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
            ImageUrl =
                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=category-thumbnails%2FCollectible%20Toys.png&version_id=null",
            CreatedAt = now
        };

        var sneaker = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Sneaker",
            Description = "Danh mục giày sneaker thời trang.",
            ImageUrl =
                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=category-thumbnails%2Fsneakers.png&version_id=null",
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
            }
        };

        // Thêm vào context
        await _context.Categories.AddRangeAsync(collectibleToys, sneaker);
        await _context.Categories.AddRangeAsync(children);
        await _context.SaveChangesAsync();

        await _cacheService.RemoveByPatternAsync("category:all");

        _logger.Success("[SeedCategories] Seed danh mục thành công.");
    }

    private async Task SeedProducts()
    {
        var sellerUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "blindtreasurefpt@gmail.com");
        if (sellerUser == null)
        {
            _logger.Error("Không tìm thấy user Seller với email blindtreasurefpt@gmail.com để tạo product.");
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
                            ImageUrls = new List<string>
                            {
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FBaby%20Molly%20Funny%20Raining%20Day%20Figure.jpg&version_id=null"
                            },
                            Brand = seller.CompanyName,
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
                            ImageUrls = new List<string>
                            {
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FHand%20in%20Hand%20Series%20Figures.jpg&version_id=null"
                            },
                            Brand = seller.CompanyName,
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
                            ImageUrls = new List<string>
                            {
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FCHAKA%20Candle%20Whisper%20Series%20Figures.jpg&version_id=null"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 6
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "THE MONSTERS Let's Checkmate Series-Vinyl Plush Doll",
                            Description = "Blind box PopMart kích thước nhỏ gọn, thích hợp sưu tầm.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 200000,
                            Stock = 70,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                //img 1
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll.png&version_id=null",
                                // img 2
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%20(2).jpg&version_id=null",
                                //img 3
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%20(1).jpg&version_id=null"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 6
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "THE MONSTERS Let's Checkmate Series-Vinyl Plush Doll",
                            Description = "Blind box PopMart kích thước nhỏ gọn, thích hợp sưu tầm.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 3000000,
                            Stock = 70,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                //img 1
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll.png&version_id=null",
                                // img 2
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%20(2).jpg&version_id=null",
                                //img 3
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fpopmarts%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%2FTHE%20MONSTERS%20Let%27s%20Checkmate%20Series-Vinyl%20Plush%20Doll%20(1).jpg&version_id=null"
                            },
                            Brand = seller.CompanyName,
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
                            Name = "Búp Bê Baby Three V3 Check Card Blindbox Thỏ Màu Hồng",
                            Description =
                                "Búp Bê Baby Three V3 Check Card Blindbox Thỏ Màu Hồng là món đồ chơi giải trí được nhiều bạn trẻ yêu thích và săn đón hiện nay. Món đồ chơi này được lấy hình tượng từ nhân vật hoạt hình quen thuộc trong cuộc sống với thiết kế kiểu dáng đáng yêu, ngộ nghĩnh và có chút cá tính. Với chất liệu bền đẹp cùng tính ứng dụng cao, búp bê Baby Three luôn nhận được sự yêu thích của người dùng. ",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 990000,
                            Stock = 40,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FB%C3%BAp%20B%C3%AA%20Baby%20Three%20V3%20Check%20Card%20Blindbox%20Th%E1%BB%8F%20M%C3%A0u%20H%E1%BB%93ng.png&version_id=null"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 9
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Búp Bê Baby Three V3 Vinyl Plush Dinosaur Màu Xanh Lá",
                            Description =
                                "Búp Bê Baby Three V3 Vinyl Plush Dinosaur Màu Xanh Lá là món đồ chơi giải trí được nhiều bạn trẻ yêu thích và săn đón hiện nay. Vinyl Plush Dinosaur được lấy hình tượng từ chú khủng long xanh lạ mắt, thiết kế với kiểu dáng đáng yêu, ngộ nghĩnh và có chút cá tính. Với chất liệu bền đẹp cùng tính ứng dụng cao, búp bê Baby Three luôn nhận được sự yêu thích của người dùng. ",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 550000,
                            Stock = 25,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FB%C3%BAp%20B%C3%AA%20Baby%20Three%20V3%20Vinyl%20Plush%20Dinosaur%20M%C3%A0u%20Xanh%20L%C3%A1%2Fbup-be-baby-three-v3-vinyl-plush-dinosaur-mau-xanh-la-66f4e3b343e26-26092024113147.png&version_id=null",
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FB%C3%BAp%20B%C3%AA%20Baby%20Three%20V3%20Vinyl%20Plush%20Dinosaur%20M%C3%A0u%20Xanh%20L%C3%A1%2Fbup-be-baby-three-v3-vinyl-plush-dinosaur-mau-xanh-la-66f4e3b346bef-26092024113147.png&version_id=null"
                            },
                            Brand = seller.CompanyName,
                            Material = "Fabric",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 15
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Búp Bê Baby Three Chinese Zodiac Plush Doll Blind Box Mặt Dâu Màu Hồng",
                            Description =
                                "Búp Bê Baby Three Chinese Zodiac Plush Doll Blind Box Mặt Dâu Màu Hồng là món đồ chơi giải trí được nhiều bạn trẻ yêu thích và săn đón hiện nay. Zodiac Plush Doll Mặt Dâu được lấy hình tượng từ nhân vật hoạt hình quen thuộc trong cuộc sống với thiết kế kiểu dáng đáng yêu, ngộ nghĩnh và có chút cá tính. Với chất liệu bền đẹp cùng tính ứng dụng cao, búp bê Baby Three luôn nhận được sự yêu thích của người dùng. ",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 1190000,
                            Stock = 10,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FB%C3%BAp%20B%C3%AA%20Baby%20Three%20Chinese%20Zodiac%20Plush%20Doll%20Blind%20Box%20M%E1%BA%B7t%20D%C3%A2u%20M%C3%A0u%20H%E1%BB%93ng.png&version_id=null"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 11
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "Yooki Lamb 400% Vinyl Plush Doll ( Chính Hãng )",
                            Description = "Blind box PopMart kích thước nhỏ gọn, thích hợp sưu tầm.",
                            CategoryId = category.Id,
                            SellerId = seller.Id,
                            Price = 750000,
                            Stock = 70,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                //img 1
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%20(1).webp&version_id=null",
                                // img 2
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%20(2).webp&version_id=null",
                                //img 3
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%20(3).webp&version_id=null",
                                //img 4
                                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=products%2Fbabythree%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%2FYooki%20Lamb%20400%25%20Vinyl%20Plush%20Doll%20(%20Ch%C3%ADnh%20H%C3%A3ng%20)%20(4).webp&version_id=null"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 30
                        }
                    });
                    break;

                default:
                    _logger.Warn($"Chưa có dữ liệu mẫu cho category {category.Name}, bỏ qua tạo sản phẩm.");
                    break;
            }

        if (products.Count == 0)
        {
            _logger.Warn("[SeedProducts] Không có sản phẩm nào được tạo do không có category con phù hợp.");
            return;
        }

        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();
        _logger.Success("[SeedProducts] Seed sản phẩm chuẩn thành công.");
    }

    private async Task SeedBlindBoxes()
    {
        var now = DateTime.UtcNow;

        var sellerUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "blindtreasurefpt@gmail.com");
        if (sellerUser == null)
        {
            _logger.Error("Không tìm thấy user Seller với email blindtreasurefpt@gmail.com để tạo blind box.");
            return;
        }

        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == sellerUser.Id);
        if (seller == null)
        {
            _logger.Error("User này chưa có Seller tương ứng.");
            return;
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "PopMart" && !c.IsDeleted);
        if (category == null)
        {
            _logger.Error("Không tìm thấy category PopMart để gán cho sản phẩm.");
            return;
        }

        // === Tạo sản phẩm riêng biệt chỉ dùng cho blind box ===
        var blindBoxProducts = new List<Product>
        {
            //prod 1
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HACIPUPU Snuggle With You Series Figure Blind Box",
                Description = "Mô hình thỏ hồng phiên bản đặc biệt cho blindbox.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 320000,
                Stock = 40,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2Fca-sau-sao-chep.webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 12
            },
            //prod 2
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HACIPUPU Snuggle With You Series Figure Blind Box",
                Description = "Mô hình thỏ hồng phiên bản đặc biệt cho blindbox.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 320000,
                Stock = 40,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2Fcanh-cut-sao-chep.webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 12
            },
            //prod 3
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HACIPUPU Snuggle With You Series Figure Blind Box",
                Description = "Mô hình thỏ hồng phiên bản đặc biệt cho blindbox.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 320000,
                Stock = 40,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2Fheo-hong-sao-chep.jpg&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 12
            },
            //prod 4
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HACIPUPU Snuggle With You Series Figure Blind Box",
                Description = "Mô hình thỏ hồng phiên bản đặc biệt cho blindbox.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 320000,
                Stock = 40,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2Fkhung-moi-khong-website-sao-chep.webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 12
            },
            //prod 5
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HACIPUPU Snuggle With You Series Figure Blind Box",
                Description = "Mô hình thỏ hồng phiên bản đặc biệt cho blindbox.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 320000,
                Stock = 40,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2Ftim-sao-chep%20(1).webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 12
            },
            //prod 6
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HACIPUPU Snuggle With You Series Figure Blind Box",
                Description = "Mô hình thỏ hồng phiên bản đặc biệt cho blindbox.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 320000,
                Stock = 40,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2Fheo-sao-chep.webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 12
            }
        };

        await _context.Products.AddRangeAsync(blindBoxProducts);
        await _context.SaveChangesAsync();

        // === Tạo blind box và các item ===
        var blindBox = new BlindBox
        {
            Id = Guid.NewGuid(),
            SellerId = seller.Id,
            CategoryId = category.Id,
            Name = "HACIPUPU Snuggle With You Series Figure Blind Box",
            Description = "Blindbox phiên bản đặc biệt chứa các mô hình giới hạn.",
            Price = 500000,
            TotalQuantity = 30,
            HasSecretItem = true,
            SecretProbability = 15,
            Status = BlindBoxStatus.Approved,
            ImageUrl =
                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box.webp&version_id=null",
            ReleaseDate = now,
            CreatedAt = now
        };

        var blindBoxItems = new List<BlindBoxItem>();

        for (var i = 0; i < blindBoxProducts.Count; i++)
        {
            var product = blindBoxProducts[i];

            blindBoxItems.Add(new BlindBoxItem
            {
                Id = Guid.NewGuid(),
                BlindBoxId = blindBox.Id,
                ProductId = product.Id,
                Quantity = 15,
                DropRate = i == 1 ? 15m : 85m, // sản phẩm thứ 2 là secret
                Rarity = i == 1 ? BlindBoxRarity.Secret : BlindBoxRarity.Common,
                IsActive = true,
                CreatedAt = now
            });
        }

        await _context.BlindBoxes.AddAsync(blindBox);
        await _context.BlindBoxItems.AddRangeAsync(blindBoxItems);
        await _context.SaveChangesAsync();

        _logger.Success("Seed blindbox và sản phẩm riêng biệt thành công.");
    }

    #endregion


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
                Email = "trangiaphuc362003181@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Trần Gia Phúc",
                Phone = "0900000002",
                Status = UserStatus.Active,
                RoleName = RoleType.Customer,
                CreatedAt = now
            },
            new()
            {
                Email = "staff@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Nhân viên năng suất ",
                Phone = "0900000003",
                Status = UserStatus.Active,
                RoleName = RoleType.Staff,
                CreatedAt = now
            },
            new()
            {
                Email = "admin@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Admin Đẹp Trai ",
                Phone = "0900000004",
                Status = UserStatus.Active,
                RoleName = RoleType.Admin,
                CreatedAt = now
            },
            new()
            {
                Email = "blindtreasurefpt@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Official Brand Seller",
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