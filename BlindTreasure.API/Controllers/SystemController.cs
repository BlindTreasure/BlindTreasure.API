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
            await SeedPromotions();
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

    [HttpPost("dev/seed-user-blind-boxes")]
    public async Task<IActionResult> SeedBlindBoxUsers()
    {
        try
        {
            var email = "trangiaphuc362003181@gmail.com";

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return NotFound($"Không tìm thấy user với email: {email}");

            // First create a special blind box with high secret rate
            var specialBlindBox = await SeedSpecialBlindBox(user.Id);

            // Then assign regular blind boxes
            var blindBoxes = await _context.BlindBoxes
                .Include(b => b.BlindBoxItems!)
                .ThenInclude(i => i.ProbabilityConfigs!)
                .Where(b => b.Status == BlindBoxStatus.Approved && !b.IsDeleted &&
                            b.Id != specialBlindBox.Id) // Exclude our special box
                .ToListAsync();

            var validBlindBoxes = blindBoxes
                .Where(b => b.BlindBoxItems != null && b.BlindBoxItems.Any(i =>
                    !i.IsDeleted &&
                    i.IsActive &&
                    i.Quantity > 0 &&
                    i.ProbabilityConfigs.Any(p =>
                        p.EffectiveFrom <= DateTime.UtcNow &&
                        p.EffectiveTo >= DateTime.UtcNow)))
                .OrderBy(_ => Guid.NewGuid())
                .Take(2)
                .ToList();

            if (!validBlindBoxes.Any())
                return BadRequest("Không tìm thấy blind box hợp lệ để seed.");

            var customerBoxes = validBlindBoxes.Select(b => new CustomerBlindBox
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                BlindBoxId = b.Id,
                IsOpened = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            // Add our special box to the list
            customerBoxes.Add(new CustomerBlindBox
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                BlindBoxId = specialBlindBox.Id,
                IsOpened = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.CustomerBlindBoxes.AddRangeAsync(customerBoxes);
            await _context.SaveChangesAsync();

            return Ok(ApiResult<object>.Success("200",
                $"Đã seed {customerBoxes.Count} hộp cho user {user.Email}, bao gồm 1 hộp đặc biệt có tỉ lệ secret cao."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
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
            await _cacheService.RemoveByPatternAsync("inventoryitem:");
            await _cacheService.RemoveByPatternAsync("Promotion:");


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
                    () => context.RarityConfigs.ExecuteDeleteAsync(),
                    () => context.BlindBoxItems.ExecuteDeleteAsync(),
                    () => context.CartItems.ExecuteDeleteAsync(),
                    () => context.OrderDetails.ExecuteDeleteAsync(),
                    () => context.Listings.ExecuteDeleteAsync(),
                    () => context.InventoryItems.ExecuteDeleteAsync(),
                    () => context.CustomerBlindBoxes.ExecuteDeleteAsync(),
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
                    () => context.PromotionParticipants.ExecuteDeleteAsync(),
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

        await SeedAddressesForUsers(users);


        await SeedSellerForUser("blindtreasurefpt@gmail.com");
        await SeedSellerForUser("hanhnthse170189@fpt.edu.vn");

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
                Name = "Baby Three",
                Description = "Mẫu đồ chơi sưu tầm dòng Baby Three.",
                ParentId = collectibleToys.Id,
                CreatedAt = now
            }
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

        // Seed purchased products for customer
        await SeedPurchasedProducts(products);
    }

    private async Task SeedPurchasedProducts(List<Product> products)
    {
        // Find the customer user
        var customer = await _context.Users.FirstOrDefaultAsync(u => u.Email == "trangiaphuc362003181@gmail.com");
        if (customer == null)
        {
            _logger.Error("[SeedPurchasedProducts] Không tìm thấy user customer để tạo purchased products.");
            return;
        }

        // Get customer's default address
        var defaultAddress = await _context.Addresses
            .FirstOrDefaultAsync(a => a.UserId == customer.Id && a.IsDefault);

        if (defaultAddress == null)
        {
            _logger.Warn("[SeedPurchasedProducts] Không tìm thấy địa chỉ mặc định của customer.");
        }

        // Select 4 products to mark as purchased (2 from each category if possible)
        var purchasedProducts = products
            .GroupBy(p => p.CategoryId)
            .SelectMany(g => g.Take(2))
            .Take(4)
            .ToList();

        if (!purchasedProducts.Any())
        {
            _logger.Warn("[SeedPurchasedProducts] Không có sản phẩm nào để đánh dấu là đã mua.");
            return;
        }

        var now = DateTime.UtcNow;
        var inventoryItems = new List<InventoryItem>();

        foreach (var product in purchasedProducts)
        {
            inventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                ProductId = product.Id,
                Quantity = 1,
                Location = "HCM",
                Status = InventoryItemStatus.Available,
                IsFromBlindBox = false,
                AddressId = defaultAddress?.Id,
                CreatedAt = now.AddDays(-Random.Shared.Next(1, 30)), // Random purchase date in the last month
            });
        }

        await _context.InventoryItems.AddRangeAsync(inventoryItems);
        await _context.SaveChangesAsync();

        _logger.Success(
            $"[SeedPurchasedProducts] Đã seed {inventoryItems.Count} sản phẩm đã mua cho user {customer.Email}.");
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

        // Lấy tất cả category con (ParentId != null)
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted && c.ParentId != null)
            .ToListAsync();

        if (!categories.Any())
        {
            _logger.Warn("[SeedBlindBoxes] Không tìm thấy category con để tạo blind box.");
            return;
        }

        foreach (var category in categories)
        {
            // Tạo mới 6 sản phẩm cho mỗi category, ProductSaleType là BlindBoxOnly
            var blindBoxProducts = new List<Product>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"HACIPUPU Snuggle With You Series Figure 1 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox.",
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
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"HACIPUPU Snuggle With You Series Figure 2 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox.",
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
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"HACIPUPU Snuggle With You Series Figure 3 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox.",
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
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"HACIPUPU Snuggle With You Series Figure 4 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox.",
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
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"HACIPUPU Snuggle With You Series Figure 5 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox.",
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
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"HACIPUPU Snuggle With You Series Figure 6 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox.",
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

            // Rarity cấu hình cho từng item (chuẩn theo enum RarityName)
            var rarityArr = new[]
            {
                new { Rarity = RarityName.Common, Weight = 40, Quantity = 10 },
                new { Rarity = RarityName.Rare, Weight = 25, Quantity = 8 },
                new { Rarity = RarityName.Rare, Weight = 10, Quantity = 8 },
                new { Rarity = RarityName.Epic, Weight = 10, Quantity = 5 },
                new { Rarity = RarityName.Epic, Weight = 10, Quantity = 4 },
                new { Rarity = RarityName.Secret, Weight = 5, Quantity = 2 }
            };

            // Tổng quantity * weight để tính drop rate
            var totalWeightQty = rarityArr.Sum(x => x.Quantity * x.Weight);

            var blindBox = new BlindBox
            {
                Id = Guid.NewGuid(),
                SellerId = seller.Id,
                CategoryId = category.Id,
                Name = $"Blind Box - {category.Name}",
                Description = $"Blindbox mẫu chứa 6 sản phẩm thuộc {category.Name}",
                Price = 500000,
                TotalQuantity = 30,
                HasSecretItem = true,
                SecretProbability = 5,
                Status = BlindBoxStatus.Approved,
                ImageUrl = blindBoxProducts[0].ImageUrls.FirstOrDefault() ?? "",
                ReleaseDate = now,
                CreatedAt = now
            };

            var blindBoxItems = new List<BlindBoxItem>();
            var rarityConfigs = new List<RarityConfig>();

            for (var i = 0; i < 6; i++)
            {
                var r = rarityArr[i];
                var product = blindBoxProducts[i];

                var dropRate = Math.Round((decimal)(r.Quantity * r.Weight) / totalWeightQty * 100m, 2);
                var itemId = Guid.NewGuid();

                blindBoxItems.Add(new BlindBoxItem
                {
                    Id = itemId,
                    BlindBoxId = blindBox.Id,
                    ProductId = product.Id,
                    Quantity = r.Quantity,
                    DropRate = dropRate,
                    IsSecret = r.Rarity == RarityName.Secret,
                    IsActive = true,
                    CreatedAt = now
                });

                rarityConfigs.Add(new RarityConfig
                {
                    Id = Guid.NewGuid(),
                    BlindBoxItemId = itemId,
                    Name = r.Rarity,
                    Weight = r.Weight,
                    IsSecret = r.Rarity == RarityName.Secret,
                    CreatedAt = now
                });
            }

            await _context.BlindBoxes.AddAsync(blindBox);
            await _context.BlindBoxItems.AddRangeAsync(blindBoxItems);
            await _context.RarityConfigs.AddRangeAsync(rarityConfigs);
            await _context.SaveChangesAsync();

            // Sau khi SaveChanges xong BlindBox và BlindBoxItems:
            foreach (var item in blindBoxItems)
            {
                var probCfg = new ProbabilityConfig
                {
                    Id = Guid.NewGuid(),
                    BlindBoxItemId = item.Id,
                    Probability = item.DropRate,
                    EffectiveFrom = now,
                    EffectiveTo = now.AddYears(1), // đảm bảo NOW nằm trong range này
                    ApprovedBy = sellerUser.Id, // hoặc Id của user staff test (nếu có)
                    ApprovedAt = now,
                    CreatedAt = now
                };
                await _context.ProbabilityConfigs.AddAsync(probCfg);
            }

            await _context.SaveChangesAsync();


            _logger.Success($"[SeedBlindBoxes] Đã seed blind box cho category {category.Name} thành công.");
        }
    }

    private async Task SeedPromotions()
    {
        if (_context.Promotions.Any())
        {
            _logger.Info("[SeedPromotions] Đã tồn tại promotions. Bỏ qua seed.");
            return;
        }

        var now = DateTime.UtcNow;

        // Lấy seller mẫu
        var seller = await _context.Sellers.FirstOrDefaultAsync();
        if (seller == null)
        {
            _logger.Warn("[SeedPromotions] Không tìm thấy Seller để tạo promotion.");
            return;
        }

        var promotions = new List<Promotion>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "SALE10",
                Description = "Giảm 10% cho tất cả đơn hàng.",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 10,
                StartDate = now,
                EndDate = now.AddMonths(1),
                Status = PromotionStatus.Approved,
                SellerId = seller.Id,
                UsageLimit = 200,
                CreatedByRole = RoleType.Staff,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "FREESH",
                Description = "Giảm 50K cho đơn từ 500K.",
                DiscountType = DiscountType.Fixed,
                DiscountValue = 50000,
                StartDate = now,
                EndDate = now.AddMonths(2),
                Status = PromotionStatus.Pending,
                SellerId = seller.Id,
                UsageLimit = 100,
                CreatedByRole = RoleType.Seller,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "LIMITED",
                Description = "Voucher giới hạn cho khách VIP.",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 15,
                StartDate = now,
                EndDate = now.AddMonths(1),
                Status = PromotionStatus.Approved,
                SellerId = seller.Id,
                UsageLimit = 10,
                CreatedByRole = RoleType.Staff,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "REJECT",
                Description = "Voucher test bị từ chối.",
                DiscountType = DiscountType.Fixed,
                DiscountValue = 30000,
                StartDate = now,
                EndDate = now.AddMonths(1),
                Status = PromotionStatus.Rejected,
                SellerId = seller.Id,
                RejectReason = "Sai thông tin khuyến mãi",
                UsageLimit = 20,
                CreatedByRole = RoleType.Seller,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "GLOBAL1",
                Description = "Voucher toàn sàn cho mọi khách hàng.",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 5,
                StartDate = now,
                EndDate = now.AddMonths(1),
                Status = PromotionStatus.Approved,
                SellerId = null, // Toàn sàn
                UsageLimit = null,
                CreatedByRole = RoleType.Staff,
                CreatedAt = now
            }
        };

        await _context.Promotions.AddRangeAsync(promotions);
        await _context.SaveChangesAsync();
        _logger.Success("[SeedPromotions] Đã seed 5 promotion mẫu.");
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

    private async Task SeedAddressesForUsers(List<User> users)
    {
        var now = DateTime.UtcNow;
        var addresses = new List<Address>();

        // Lọc user role = Customer
        var customerUsers = users.Where(u => u.RoleName == RoleType.Customer);

        foreach (var user in customerUsers)
        {
            addresses.Add(new Address
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FullName = user.FullName,
                Phone = user.Phone ?? "0900000000",
                AddressLine = "181 Nguyễn Văn Nghi, Phường 7",
                City = "TP Hồ Chí Minh",
                Province = "Quận Gò Vấp",
                PostalCode = "",
                Country = "Việt Nam",
                IsDefault = true,
                CreatedAt = now,
                CreatedBy = user.Id
            });
        }

        if (addresses.Any())
        {
            await _context.Addresses.AddRangeAsync(addresses);
            await _context.SaveChangesAsync();
        }
    }


    private List<User> GetPredefinedUsers()
    {
        var passwordHasher = new PasswordHasher();
        var now = DateTime.UtcNow;
        var defaultAvatar = "https://img.freepik.com/free-psd/3d-illustration-human-avatar-profile_23-2150671142.jpg";

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
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "quanghnse170229@fpt.edu.vn",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Qang",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "staff@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Nhân viên năng suất ",
                Phone = "0900000003",
                Status = UserStatus.Active,
                RoleName = RoleType.Staff,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "admin@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Admin Đẹp Trai ",
                Phone = "0900000004",
                Status = UserStatus.Active,
                RoleName = RoleType.Admin,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "blindtreasurefpt@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Official Brand Seller",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "hanhnthse170189@fpt.edu.vn",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Official Brand Seller",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
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

    private async Task<BlindBox> SeedSpecialBlindBox(Guid userId)
    {
        var now = DateTime.UtcNow;

        // Find the seller
        var sellerUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "blindtreasurefpt@gmail.com");
        if (sellerUser == null)
        {
            throw new Exception("Không tìm thấy seller để tạo special blind box");
        }

        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == sellerUser.Id);
        if (seller == null)
        {
            throw new Exception("Không tìm thấy seller info để tạo special blind box");
        }

        // Get first category
        var category = await _context.Categories
            .Where(c => !c.IsDeleted && c.ParentId != null)
            .FirstOrDefaultAsync();

        if (category == null)
        {
            throw new Exception("Không tìm thấy category để tạo special blind box");
        }

        // Create special blind box products
        var specialProducts = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "LEGENDARY TREASURE Mythical Golden Dragon",
                Description = "Mô hình rồng vàng cực hiếm, phiên bản giới hạn dành cho BlindBox đặc biệt.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 1500000,
                Stock = 1,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box.webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "Gold Plated PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 15
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "LEGENDARY TREASURE Phoenix Fire Bird",
                Description = "Mô hình phượng hoàng lửa cực hiếm, phiên bản giới hạn dành cho BlindBox đặc biệt.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 1200000,
                Stock = 1,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box.webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "Crystal PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 15
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "LEGENDARY TREASURE Crystal Unicorn",
                Description = "Mô hình kì lân pha lê, phiên bản giới hạn dành cho BlindBox đặc biệt.",
                CategoryId = category.Id,
                SellerId = seller.Id,
                Price = 980000,
                Stock = 2,
                Status = ProductStatus.Active,
                CreatedAt = now,
                ImageUrls = new List<string>
                {
                    "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box.webp&version_id=null"
                },
                Brand = seller.CompanyName,
                Material = "Crystal PVC",
                ProductType = ProductSaleType.BlindBoxOnly,
                Height = 12
            }
        };

        await _context.Products.AddRangeAsync(specialProducts);
        await _context.SaveChangesAsync();

        // Create the special blind box with high secret rate
        var specialBlindBox = new BlindBox
        {
            Id = Guid.NewGuid(),
            SellerId = seller.Id,
            CategoryId = category.Id,
            Name = "LEGENDARY TREASURE BOX",
            Description =
                "Hộp quà đặc biệt với tỉ lệ trúng vật phẩm hiếm cao nhất! Chỉ giới hạn cho các thành viên đặc biệt.",
            Price = 2000000,
            TotalQuantity = 5,
            HasSecretItem = true,
            SecretProbability = 30, // Tỉ lệ secret cao gấp 6 lần bình thường
            Status = BlindBoxStatus.Approved,
            ImageUrl =
                "https://minio.fpt-devteam.fun/api/v1/buckets/blindtreasure-bucket/objects/download?preview=true&prefix=blindbox-thumbnails%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box%2FHACIPUPU%20Snuggle%20With%20You%20Series%20Figure%20Blind%20Box.webp&version_id=null",
            ReleaseDate = now,
            CreatedAt = now
        };

        await _context.BlindBoxes.AddAsync(specialBlindBox);
        await _context.SaveChangesAsync();

        // Configure rarity for the special items
        var blindBoxItems = new List<BlindBoxItem>();
        var rarityConfigs = new List<RarityConfig>();

        // Define special rarity configuration with high secret rates
        var specialRarityArr = new[]
        {
            new
            {
                Rarity = RarityName.Secret, Weight = 60, Quantity = 1, Product = specialProducts[0]
            }, // Golden Dragon
            new { Rarity = RarityName.Secret, Weight = 30, Quantity = 1, Product = specialProducts[1] }, // Phoenix
            new { Rarity = RarityName.Epic, Weight = 10, Quantity = 2, Product = specialProducts[2] } // Unicorn
        };

        // Total weight calculation
        var totalWeightQty = specialRarityArr.Sum(x => x.Quantity * x.Weight);

        for (var i = 0; i < specialRarityArr.Length; i++)
        {
            var r = specialRarityArr[i];
            var product = r.Product;

            var dropRate = Math.Round((decimal)(r.Quantity * r.Weight) / totalWeightQty * 100m, 2);
            var itemId = Guid.NewGuid();

            blindBoxItems.Add(new BlindBoxItem
            {
                Id = itemId,
                BlindBoxId = specialBlindBox.Id,
                ProductId = product.Id,
                Quantity = r.Quantity,
                DropRate = dropRate,
                IsSecret = r.Rarity == RarityName.Secret,
                IsActive = true,
                CreatedAt = now
            });

            rarityConfigs.Add(new RarityConfig
            {
                Id = Guid.NewGuid(),
                BlindBoxItemId = itemId,
                Name = r.Rarity,
                Weight = r.Weight,
                IsSecret = r.Rarity == RarityName.Secret,
                CreatedAt = now
            });
        }

        await _context.BlindBoxItems.AddRangeAsync(blindBoxItems);
        await _context.RarityConfigs.AddRangeAsync(rarityConfigs);
        await _context.SaveChangesAsync();

        // Add probability configs
        foreach (var item in blindBoxItems)
        {
            var probCfg = new ProbabilityConfig
            {
                Id = Guid.NewGuid(),
                BlindBoxItemId = item.Id,
                Probability = item.DropRate,
                EffectiveFrom = now,
                EffectiveTo = now.AddYears(1),
                ApprovedBy = sellerUser.Id,
                ApprovedAt = now,
                CreatedAt = now
            };
            await _context.ProbabilityConfigs.AddAsync(probCfg);
        }

        await _context.SaveChangesAsync();
        _logger.Success("[SeedSpecialBlindBox] Đã tạo thành công blind box đặc biệt với tỉ lệ secret cao");

        return specialBlindBox;
    }

    #endregion
}