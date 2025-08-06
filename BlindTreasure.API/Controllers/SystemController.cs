using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain;
using BlindTreasure.Domain.DTOs.UnboxDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly IUnboxingService _unboxService;
    private readonly BlindTreasureDbContext _context;
    private readonly ILoggerService _logger;

    public SystemController(BlindTreasureDbContext context, ILoggerService logger, ICacheService cacheService,
        IUnboxingService unboxService)
    {
        _context = context;
        _logger = logger;
        _cacheService = cacheService;
        _unboxService = unboxService;
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
            await SmiskiBlindBoxes();
            await SeedPromotions();
            await SeedPromotionParticipants();
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

    /// <summary>
    /// Seed blind boxes cho user để test chức năng mở hộp
    /// </summary>
    /// <param name="userId">ID của user cần seed blind box. Nếu không truyền sẽ mặc định là user trangiaphuc362003181@gmail.com</param>
    /// <returns>Thông tin seed thành công với số lượng hộp đã tạo</returns>
    [HttpPost("dev/seed-user-blind-boxes")]
    public async Task<IActionResult> SeedBlindBoxUsers([FromQuery] Guid? userId = null)
    {
        try
        {
            User? user;

            if (userId.HasValue)
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (user == null)
                {
                    _logger.Warn($"[SeedUserBoxes] Không tìm thấy user với Id: {userId}");
                    return NotFound($"Không tìm thấy user với Id: {userId}");
                }

                _logger.Info($"[SeedUserBoxes] Bắt đầu seed blind box cho user Id: {userId}");
            }
            else
            {
                var email = "trangiaphuc362003181@gmail.com";
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    _logger.Warn($"[SeedUserBoxes] Không tìm thấy user với email: {email}");
                    return NotFound($"Không tìm thấy user với email: {email}");
                }

                _logger.Info($"[SeedUserBoxes] Bắt đầu seed blind box cho user: {email}");
            }

            // Gọi hàm seed hộp (nếu chưa có)
            _logger.Info("[SeedUserBoxes] Gọi SeedHighSecretBlindBoxes()");
            await SeedHighSecretBlindBoxes();

            // Lấy blind box thường
            _logger.Info("[SeedUserBoxes] Truy vấn hộp thường (SecretProbability <= 5)");
            var regularBlindBoxes = await _context.BlindBoxes
                .Include(b => b.BlindBoxItems!)
                .ThenInclude(i => i.ProbabilityConfigs!)
                .Where(b => b.Status == BlindBoxStatus.Approved && !b.IsDeleted && b.SecretProbability <= 5)
                .ToListAsync();

            var validRegularBoxes = regularBlindBoxes
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

            _logger.Info($"[SeedUserBoxes] Tìm thấy {validRegularBoxes.Count} hộp thường hợp lệ");

            // Lấy hộp có tỉ lệ secret cao
            _logger.Info("[SeedUserBoxes] Truy vấn hộp secret cao (SecretProbability == 25)");
            var highSecretBlindBoxes = await _context.BlindBoxes
                .Include(b => b.BlindBoxItems!)
                .ThenInclude(i => i.ProbabilityConfigs!)
                .Where(b => b.Status == BlindBoxStatus.Approved && !b.IsDeleted && b.SecretProbability == 25)
                .ToListAsync();

            var validHighSecretBoxes = highSecretBlindBoxes
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

            _logger.Info($"[SeedUserBoxes] Tìm thấy {validHighSecretBoxes.Count} hộp secret cao hợp lệ");

            // Tổng hợp
            var allBoxes = validRegularBoxes.Concat(validHighSecretBoxes).ToList();

            if (allBoxes.Count < 4)
            {
                _logger.Warn(
                    $"[SeedUserBoxes] Không đủ blind box hợp lệ để seed. Yêu cầu 4 hộp, hiện có {allBoxes.Count}");
                return BadRequest($"Không đủ blind box hợp lệ để seed. Cần 4 box, hiện có {allBoxes.Count} box.");
            }

            var customerBoxes = allBoxes.Select(b => new CustomerBlindBox
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                BlindBoxId = b.Id,
                IsOpened = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await _context.CustomerBlindBoxes.AddRangeAsync(customerBoxes);
            await _context.SaveChangesAsync();

            _logger.Success($"[SeedUserBoxes] Seed thành công {customerBoxes.Count} hộp cho user {user.Email}");

            return Ok(ApiResult<object>.Success("200",
                $"Đã seed {customerBoxes.Count} hộp cho user {user.Email} (2 hộp thường, 2 hộp có tỉ lệ secret cao 25%)."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            _logger.Error($"[SeedUserBoxes] Exception: {ex.Message}");
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Seed inventory items cho user bằng cách tự động mở 2 blind box ngẫu nhiên
    /// </summary>
    /// <param name="userId">ID của user cần seed inventory. Nếu không truyền sẽ mặc định là user trangiaphuc362003181@gmail.com</param>
    /// <returns>Thông tin các items đã được thêm vào inventory</returns>
    [HttpPost("dev/seed-user-inventoryItems")]
    public async Task<IActionResult> SeedUserInventory([FromQuery] Guid? userId = null)
    {
        try
        {
            User? user;

            if (userId.HasValue)
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (user == null)
                {
                    _logger.Warn($"[SeedUserInventory] Không tìm thấy user với Id: {userId}");
                    return NotFound($"Không tìm thấy user với Id: {userId}");
                }

                _logger.Info($"[SeedUserInventory] Bắt đầu seed inventory cho user Id: {userId}");
            }
            else
            {
                var email = "trangiaphuc362003181@gmail.com";
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    _logger.Warn($"[SeedUserInventory] Không tìm thấy user với email: {email}");
                    return NotFound($"Không tìm thấy user với email: {email}");
                }

                _logger.Info($"[SeedUserInventory] Bắt đầu seed inventory cho user: {email}");
            }

            // Lấy tất cả CustomerBlindBoxes chưa mở của user
            var unopenedBoxes = await _context.CustomerBlindBoxes
                .Where(cb => cb.UserId == user.Id && !cb.IsOpened && !cb.IsDeleted)
                .ToListAsync();

            if (unopenedBoxes.Count < 2)
            {
                _logger.Warn(
                    $"[SeedUserInventory] User {user.Email} không có đủ hộp chưa mở. Cần ít nhất 2 hộp, hiện có {unopenedBoxes.Count}");
                return BadRequest(
                    $"User không có đủ hộp chưa mở. Cần ít nhất 2 hộp, hiện có {unopenedBoxes.Count} hộp.");
            }

            // Random chọn 2 hộp
            var random = new Random();
            var selectedBoxes = unopenedBoxes
                .OrderBy(_ => random.Next())
                .Take(2)
                .ToList();

            _logger.Info($"[SeedUserInventory] Đã chọn {selectedBoxes.Count} hộp để unbox");

            // Tạo UnboxingService với mock ClaimsService
            var mockClaimsService = new MockClaimsService(user.Id);

            // Lấy các dependencies cần thiết từ DI container
            var loggerService = HttpContext.RequestServices.GetRequiredService<ILoggerService>();
            var unitOfWork = HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
            var currentTime = HttpContext.RequestServices.GetRequiredService<ICurrentTime>();
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();

            // Tạo instance UnboxingService mới với mock ClaimsService (đúng thứ tự tham số)
            var unboxingService = new UnboxingService(
                loggerService,
                unitOfWork,
                mockClaimsService, // Sử dụng mock thay vì service thật
                currentTime,
                notificationService
            );

            var unboxResults = new List<UnboxResultDto>();

            // Unbox từng hộp đã chọn
            foreach (var box in selectedBoxes)
                try
                {
                    _logger.Info($"[SeedUserInventory] Đang unbox hộp Id: {box.Id}");
                    var result = await unboxingService.UnboxAsync(box.Id);
                    unboxResults.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[SeedUserInventory] Lỗi khi unbox hộp Id {box.Id}: {ex.Message}");
                    // Tiếp tục với hộp tiếp theo nếu có lỗi
                }

            if (!unboxResults.Any()) return BadRequest("Không thể unbox bất kỳ hộp nào. Vui lòng kiểm tra lại.");

            // Đếm số lượng inventory items của user sau khi unbox
            var inventoryCount = await _context.InventoryItems
                .Where(ii => ii.UserId == user.Id && !ii.IsDeleted)
                .CountAsync();

            var response = new
            {
                Message = $"Đã seed {unboxResults.Count} inventory items cho user {user.Email}",
                TotalInventoryItems = inventoryCount,
                UnboxedItems = unboxResults.Select(r => new
                {
                    r.ProductId,
                    r.Rarity
                })
            };

            _logger.Success(
                $"[SeedUserInventory] Hoàn thành seed inventory cho user {user.Email}. Tổng cộng {inventoryCount} items trong kho.");

            return Ok(ApiResult<object>.Success(response, "200", "Seed inventory thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            _logger.Error($"[SeedUserInventory] Exception: {ex.Message}");
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Seed vào bảng CartItem một product và một blind box ngẫu nhiên cho một user.
    /// </summary>
    /// <param name="userId">ID của user cần seed cart items. Nếu không truyền sẽ mặc định là user trangiaphuc362003181@gmail.com</param>
    /// <returns>Thông tin các items đã được thêm vào giỏ hàng</returns>
    [HttpPost("dev/seed-user-cart-items")]
    public async Task<IActionResult> SeedUserCartItems([FromQuery] Guid? userId = null)
    {
        try
        {
            _logger.Info("[SeedUserCartItems] Bắt đầu seed cart items cho user.");
            User? user;

            if (userId.HasValue)
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (user == null)
                {
                    _logger.Warn($"[SeedUserCartItems] Không tìm thấy user với Id: {userId}");
                    return NotFound($"Không tìm thấy user với Id: {userId}");
                }

                _logger.Info($"[SeedUserCartItems] Bắt đầu seed cart items cho user Id: {userId}");
            }
            else
            {
                var email = "trangiaphuc362003181@gmail.com";
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    _logger.Warn($"[SeedUserCartItems] Không tìm thấy user với email: {email}");
                    return NotFound($"Không tìm thấy user với email: {email}");
                }

                _logger.Info($"[SeedUserCartItems] Bắt đầu seed cart items cho user: {email}");
            }

            // Remove existing cart items for the user to avoid duplicates
            var existingCartItems = _context.CartItems.Where(ci => ci.UserId == user.Id);
            if (await existingCartItems.AnyAsync())
            {
                _context.CartItems.RemoveRange(existingCartItems);
                await _context.SaveChangesAsync();
                _logger.Info($"[SeedUserCartItems] Đã xóa cart items cũ của user {user.Email}.");
            }

            // Lấy 1 product ngẫu nhiên
            var randomProduct = await _context.Products
                .Where(p => p.Status == ProductStatus.Active && p.Stock > 0 &&
                            p.ProductType == ProductSaleType.DirectSale && !p.IsDeleted)
                .OrderBy(p => Guid.NewGuid())
                .FirstOrDefaultAsync();

            if (randomProduct == null)
            {
                _logger.Warn("[SeedUserCartItems] Không tìm thấy product nào hợp lệ để thêm vào giỏ hàng.");
                return BadRequest("Không có sản phẩm nào hợp lệ để seed.");
            }

            _logger.Info($"[SeedUserCartItems] Đã chọn product: {randomProduct.Name}");

            // Lấy 1 blind box ngẫu nhiên
            var randomBlindBox = await _context.BlindBoxes
                .Where(b => b.Status == BlindBoxStatus.Approved && b.TotalQuantity > 0 && !b.IsDeleted)
                .OrderBy(b => Guid.NewGuid())
                .FirstOrDefaultAsync();

            if (randomBlindBox == null)
            {
                _logger.Warn("[SeedUserCartItems] Không tìm thấy blind box nào hợp lệ để thêm vào giỏ hàng.");
                return BadRequest("Không có blind box nào hợp lệ để seed.");
            }

            _logger.Info($"[SeedUserCartItems] Đã chọn blind box: {randomBlindBox.Name}");

            var cartItems = new List<CartItem>();

            // Tạo cart item cho product
            var productCartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProductId = randomProduct.Id,
                BlindBoxId = null,
                Quantity = 1,
                UnitPrice = randomProduct.Price,
                TotalPrice = randomProduct.Price * 1,
                AddedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            cartItems.Add(productCartItem);

            // Tạo cart item cho blind box
            var blindBoxCartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProductId = null,
                BlindBoxId = randomBlindBox.Id,
                Quantity = 1,
                UnitPrice = randomBlindBox.Price,
                TotalPrice = randomBlindBox.Price * 1,
                AddedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            cartItems.Add(blindBoxCartItem);

            await _context.CartItems.AddRangeAsync(cartItems);
            await _context.SaveChangesAsync();

            _logger.Success(
                $"[SeedUserCartItems] Seed thành công {cartItems.Count} items vào giỏ hàng của user {user.Email}.");

            return Ok(ApiResult<object>.Success("200",
                $"Đã seed {cartItems.Count} items vào giỏ hàng của user {user.Email}."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            _logger.Error($"[SeedUserCartItems] Exception: {ex.Message}");
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Giả lập user đã mua hàng để t test chức năng review
    /// </summary>
    /// <param name="userId">ID of the user to seed products. If not provided, defaults to user trangiaphuc362003181@gmail.com</param>
    [HttpPost("dev/seed-products-users")]
    public async Task<IActionResult> SeedProductsUsers([FromQuery] Guid? userId = null)
    {
        try
        {
            User? user;

            if (userId.HasValue)
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (user == null)
                {
                    _logger.Warn($"[SeedProductsUsers] User not found with Id: {userId}");
                    return NotFound($"User not found with Id: {userId}");
                }

                _logger.Info($"[SeedProductsUsers] Seeding products for user Id: {userId}");
            }
            else
            {
                var email = "trangiaphuc362003181@gmail.com";
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    _logger.Warn($"[SeedProductsUsers] User not found with email: {email}");
                    return NotFound($"User not found with email: {email}");
                }

                _logger.Info($"[SeedProductsUsers] Seeding products for user: {email}");
            }

            // Get 3 random products that can be reviewed (active, in stock)
            var products = await _context.Products
                .Where(p => p.Status == ProductStatus.Active && p.Stock > 0 && !p.IsDeleted)
                .OrderBy(p => Guid.NewGuid())
                .Take(3)
                .ToListAsync();

            if (products.Count < 3)
            {
                _logger.Warn("[SeedProductsUsers] Not enough valid products found (need at least 3)");
                return BadRequest("Not enough valid products found (need at least 3)");
            }

            var now = DateTime.UtcNow;

            // Create a new order
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Status = "Completed",
                TotalAmount = products.Sum(p => p.Price),
                FinalAmount = products.Sum(p => p.Price),
                PlacedAt = now.AddDays(-7), // Order was placed 7 days ago
                CompletedAt = now.AddDays(-5), // Completed 5 days ago
                CreatedAt = now
            };

            // Create order details and inventory items for each product
            var orderDetails = new List<OrderDetail>();
            var inventoryItems = new List<InventoryItem>();

            foreach (var product in products)
            {
                var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.Id == product.SellerId);
                if (seller == null)
                {
                    _logger.Warn($"[SeedProductsUsers] Seller not found for product {product.Id}");
                    continue;
                }

                // Create order detail
                var orderDetail = new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitPrice = product.Price,
                    TotalPrice = product.Price,
                    Status = OrderDetailItemStatus.DELIVERED,
                    SellerId = seller.Id,
                    CreatedAt = now
                };
                orderDetails.Add(orderDetail);

                // Create inventory item
                var inventoryItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ProductId = product.Id,
                    OrderDetailId = orderDetail.Id,
                    Status = InventoryItemStatus.Available,
                    IsFromBlindBox = false,
                    Location = "HCM",
                    Tier = RarityName.Common, // Default rarity for direct purchases
                    CreatedAt = now
                };
                inventoryItems.Add(inventoryItem);
            }

            if (!orderDetails.Any())
            {
                _logger.Warn("[SeedProductsUsers] No valid order details could be created");
                return BadRequest("No valid order details could be created");
            }

            // Save to database
            await _context.Orders.AddAsync(order);
            await _context.OrderDetails.AddRangeAsync(orderDetails);
            await _context.InventoryItems.AddRangeAsync(inventoryItems);
            await _context.SaveChangesAsync();

            _logger.Success(
                $"[SeedProductsUsers] Successfully seeded {orderDetails.Count} products and inventory items for user {user.Email}");

            return Ok(ApiResult<object>.Success(new
            {
                Message =
                    $"Successfully seeded {orderDetails.Count} purchased products and inventory items for user {user.Email}",
                OrderId = order.Id,
                ProductIds = orderDetails.Select(od => od.ProductId).ToList(),
                InventoryItemIds = inventoryItems.Select(ii => ii.Id).ToList()
            }));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            _logger.Error($"[SeedProductsUsers] Exception: {ex.Message}");
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Seed promotions and promotion participants.
    /// This will first clear existing promotions and participants before seeding new ones.
    /// </summary>
    /// <returns>A confirmation message.</returns>
    [HttpPost("dev/seed-promotions")]
    public async Task<IActionResult> SeedPromotionsData()
    {
        try
        {
            _logger.Info("[SeedPromotionsData] Starting to seed Promotions and PromotionParticipants.");

            _logger.Info("[SeedPromotionsData] Clearing existing PromotionParticipants and Promotions.");
            await _context.PromotionParticipants.ExecuteDeleteAsync();
            await _context.Promotions.ExecuteDeleteAsync();

            _logger.Info("[SeedPromotionsData] Seeding new Promotions.");
            await SeedPromotions();

            _logger.Info("[SeedPromotionsData] Seeding new PromotionParticipants.");
            await SeedPromotionParticipants();

            _logger.Success("[SeedPromotionsData] Promotions and participants seeded successfully.");
            return Ok(ApiResult<object>.Success("200", "Promotions and participants seeded successfully."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            _logger.Error($"[SeedPromotionsData] Exception: {ex.Message}");
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
            await _cacheService.RemoveByPatternAsync("listings:");
            await _cacheService.RemoveByPatternAsync("listing:");
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

    private List<User> GetPredefinedUsers()
    {
        var passwordHasher = new PasswordHasher();
        var now = DateTime.UtcNow;
        var defaultAvatar = "https://img.freepik.com/free-psd/3d-illustration-human-avatar-profile_23-2150671142.jpg";

        var users = new List<User>
        {
            new()
            {
                Email = "trangiaphuc362003181@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Trần Gia Phúc",
                Phone = "0354343507",
                Status = UserStatus.Active,
                RoleName = RoleType.Customer,
                CreatedAt = now,
                AvatarUrl = defaultAvatar,
                Addresses = new List<Address>
                {
                    new()
                    {
                        FullName = "Trần Gia Phúc",
                        Phone = "0354343507",
                        AddressLine = "181 Nguyễn Văn Nghi, Phường 7, Quận Gò Vấp",
                        City = "Ho Chi Minh City",
                        Ward = "Phường 7",
                        District = "Quận Gò Vấp",
                        Province = "Ho Chi Minh City",
                        PostalCode = "700000",
                        Country = "Vietnam",
                        IsDefault = true,
                        CreatedAt = now
                    }
                }
            },
            // Các user khác không có address
            new()
            {
                Email = "quanghnse170229@fpt.edu.vn",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Qang",
                Phone = "0933434357",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "staff@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Nhân viên năng suất",
                Phone = "0933434355",
                Status = UserStatus.Active,
                RoleName = RoleType.Staff,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "admin@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Admin Đẹp Trai",
                Phone = "0933434387",
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
                Phone = "0932434387",
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
            },
            new()
            {
                Email = "honhatquang1@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Hồ Nhật Quang",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Customer,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            },
            new()
            {
                Email = "smiskiofficial@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Official Smiski Brand",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller,
                CreatedAt = now,
                AvatarUrl = "https://smiski.com/e/wp-content/uploads/2018/02/news_01e-1.png"
            },
            new()
            {
                Email = "honhatquang3@gmail.com",
                Password = passwordHasher.HashPassword("1@"),
                FullName = "Official Brand Seller",
                Phone = "0900000001",
                Status = UserStatus.Active,
                RoleName = RoleType.Seller,
                CreatedAt = now,
                AvatarUrl = defaultAvatar
            }
        };

        return users;
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
                    () => context.InventoryItems.ExecuteDeleteAsync(),
                    () => context.CustomerFavourites.ExecuteDeleteAsync(),
                    () => context.ChatMessages.ExecuteDeleteAsync(),
                    () => context.BlindBoxUnboxLogs.ExecuteDeleteAsync(),
                    () => context.ProbabilityConfigs.ExecuteDeleteAsync(),
                    () => context.RarityConfigs.ExecuteDeleteAsync(),
                    () => context.BlindBoxItems.ExecuteDeleteAsync(),
                    () => context.CartItems.ExecuteDeleteAsync(),
                    () => context.OrderDetails.ExecuteDeleteAsync(),
                    () => context.Shipments.ExecuteDeleteAsync(),
                    () => context.Listings.ExecuteDeleteAsync(),
                    () => context.InventoryItems.ExecuteDeleteAsync(),
                    () => context.CustomerBlindBoxes.ExecuteDeleteAsync(),

                    () => context.TradeHistories.ExecuteDeleteAsync(),
                    () => context.TradeRequests.ExecuteDeleteAsync(),
                    () => context.SupportTickets.ExecuteDeleteAsync(),
                    () => context.Reviews.ExecuteDeleteAsync(),
                    () => context.Transactions.ExecuteDeleteAsync(),
                    () => context.Notifications.ExecuteDeleteAsync(),
                    () => context.OtpVerifications.ExecuteDeleteAsync(),

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

                foreach (var deleteFunc in tablesToDelete)
                    await deleteFunc();

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

        await SeedSellerForUser("blindtreasurefpt@gmail.com");
        await SeedSellerForUser("hanhnthse170189@fpt.edu.vn");
        await SeedSellerForUser("quanghnse170229@fpt.edu.vn");
        await SeedSellerForUser("smiskiofficial@gmail.com");
        await SeedSellerForUser("honhatquang3@gmail.com");

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
            },
            new()
            {
                Name = "Smiski",
                Description = "Mẫu đồ chơi sưu tầm dòng Smiski.",
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
        var sellerSmiski = await _context.Users.FirstOrDefaultAsync(u => u.Email == "smiskiofficial@gmail.com");
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

                case "Smiski":
                    // Find the Smiski seller
                    if (sellerSmiski == null)
                    {
                        _logger.Error(
                            "Không tìm thấy user Seller với email smiskiofficial@gmail.com để tạo Smiski products.");
                        break;
                    }

                    var smiskiSeller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == sellerSmiski.Id);
                    if (smiskiSeller == null)
                    {
                        _logger.Error("User smiskiofficial@gmail.com chưa có Seller tương ứng.");
                        break;
                    }

                    products.AddRange(new[]
                    {
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "SMISKI Blowing Bubbles",
                            Description =
                                "Hãy cùng chìm đắm trong một thế giới mộng mơ với SMISKI Blowing Bubbles. Phiên bản này khắc họa hình ảnh SMISKI đang tập trung thổi những quả bong bóng xà phòng lấp lánh, mỗi quả bóng như chứa đựng một ước mơ nhỏ bé bay lên. Với vẻ mặt ngây thơ và hành động đáng yêu, SMISKI không chỉ là một vật trang trí mà còn là nguồn cảm hứng cho những khoảnh khắc thư giãn, mang lại cảm giác bình yên và một chút phép màu cho không gian sống của bạn. Đặt SMISKI ở góc làm việc hay đầu giường, và để những 'quả bóng mơ ước' này nhắc nhở bạn về niềm vui trong những điều giản dị.",
                            CategoryId = category.Id,
                            SellerId = smiskiSeller.Id,
                            Price = 299000,
                            Stock = 40,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://smiski.com/e/wp-content/uploads/2025/02/img_product_sunday01.png"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 9
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "SMISKI Paper Airplane",
                            Description =
                                "Cùng SMISKI Paper Airplane nuôi dưỡng những giấc mơ bay cao, bay xa. Hình ảnh SMISKI cẩn thận cầm trên tay chiếc máy bay giấy, ánh mắt hướng về một chân trời vô định, thể hiện cho khát khao khám phá và chinh phục những vùng đất mới. Đây không chỉ là một món đồ chơi, mà là biểu tượng của lòng dũng cảm, của những hoài bão tuổi trẻ và niềm tin vào một tương lai rộng mở. Hãy để SMISKI Paper Airplane trên bàn làm việc của bạn như một lời nhắc nhở rằng không có giấc mơ nào là quá lớn và không có hành trình nào là không thể.",
                            CategoryId = category.Id,
                            SellerId = smiskiSeller.Id,
                            Price = 255000,
                            Stock = 25,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://smiski.com/e/wp-content/uploads/2025/02/img_product_sunday02.png"
                            },
                            Brand = seller.CompanyName,
                            Material = "Fabric",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 15
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "SMISKI Sunbathing",
                            Description =
                                "Tận hưởng sự bình yên tuyệt đối cùng SMISKI Sunbathing. Trong một buổi chiều nắng đẹp, SMISKI nằm dài thư giãn, trên bụng là một chú mèo nhỏ đang say ngủ, tạo nên một bức tranh hoàn hảo về tình bạn và sự tin tưởng. Vẻ mặt mãn nguyện của SMISKI và sự yên bình của khung cảnh sẽ giúp bạn tạm gác lại những lo toan, bộn bề của cuộc sống. Sản phẩm này là một lời nhắn nhủ ngọt ngào, rằng hạnh phúc đôi khi chỉ đơn giản là được ở bên cạnh người mình yêu thương, cùng nhau tận hưởng những giây phút tĩnh lặng.",
                            CategoryId = category.Id,
                            SellerId = smiskiSeller.Id,
                            Price = 319000,
                            Stock = 10,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://smiski.com/e/wp-content/uploads/2025/02/img_product_sunday03.png"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 11
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "SMISKI Sing-Along",
                            Description =
                                "Lắng nghe giai điệu của tâm hồn cùng SMISKI Sing-Along. Với cây đàn guitar trên tay, SMISKI say sưa trong thế giới âm nhạc của riêng mình, mỗi nốt nhạc vang lên là một cảm xúc, một câu chuyện được kể. Bức tượng này không chỉ dành cho những người yêu âm nhạc, mà còn dành cho những ai tìm kiếm sự đồng điệu và niềm vui trong cô đơn. SMISKI Sing-Along sẽ là người bạn tâm giao thầm lặng, cùng bạn chia sẻ mọi khoảnh khắc, biến những góc nhỏ trong nhà bạn thành một sân khấu của cảm xúc và sự sáng tạo.",
                            CategoryId = category.Id,
                            SellerId = smiskiSeller.Id,
                            Price = 275000,
                            Stock = 70,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://smiski.com/e/wp-content/uploads/2025/02/img_product_sunday04.png"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 30
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "SMISKI Skateboarding",
                            Description =
                                "Giải phóng năng lượng và cá tính của bạn cùng SMISKI Skateboarding. SMISKI xuất hiện với vẻ ngoài cực ngầu trên chiếc ván trượt, sẵn sàng cho những cú lướt điệu nghệ trên mọi địa hình. Đây là hình ảnh đại diện cho tuổi trẻ, sự năng động và tinh thần không ngại thử thách. Dù bạn là một skater hay chỉ đơn giản là người yêu thích sự tự do, SMISKI Skateboarding chắc chắn sẽ truyền cảm hứng để bạn sống hết mình, phá vỡ mọi giới hạn và tự tin thể hiện phong cách riêng.",
                            CategoryId = category.Id,
                            SellerId = smiskiSeller.Id,
                            Price = 319000,
                            Stock = 10,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://smiski.com/e/wp-content/uploads/2025/02/img_product_sunday05.png"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 11
                        },
                        new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = "SMISKI Gardening",
                            Description =
                                "Khám phá niềm vui từ việc chăm sóc và nuôi dưỡng sự sống với SMISKI Gardening. Với bình tưới trên tay, SMISKI chăm chú tưới nước cho một mầm cây nhỏ, biểu tượng của sự khởi đầu và hy vọng. Điều thú vị và hài hước nhất là một mầm xanh cũng đang nhú lên từ chính đỉnh đầu của SMISKI, như một phần thưởng cho tình yêu thiên nhiên của nó. Sản phẩm này là món quà tuyệt vời cho những người yêu cây cối, mang lại thông điệp về sự kiên nhẫn và niềm hạnh phúc khi thấy công sức của mình đơm hoa kết trái.",
                            CategoryId = category.Id,
                            SellerId = smiskiSeller.Id,
                            Price = 275000,
                            Stock = 70,
                            Status = ProductStatus.Active,
                            CreatedAt = now,
                            ImageUrls = new List<string>
                            {
                                "https://smiski.com/e/wp-content/uploads/2025/02/img_product_sunday06.png"
                            },
                            Brand = seller.CompanyName,
                            Material = "PVC",
                            ProductType = ProductSaleType.DirectSale,
                            Height = 30
                        }
                    });
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

    private async Task SmiskiBlindBoxes()
    {
        var now = DateTime.UtcNow;
        var sellerUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "smiskiofficial@gmail.com");
        if (sellerUser == null)
        {
            _logger.Error("Không tìm thấy user Seller để tạo blind box.");
            return;
        }

        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == sellerUser.Id);
        if (seller == null)
        {
            _logger.Error("User này chưa có Seller tương ứng.");
            return;
        }

        // Lấy category Smiski
        var smiskiCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.Name == "Smiski" && !c.IsDeleted);

        if (smiskiCategory == null)
        {
            _logger.Warn("[SmiskiBlindBoxes] Không tìm thấy category 'Smiski' để tạo blind box.");
            return;
        }

        // Check if the blind box already exists
        if (await _context.BlindBoxes.AnyAsync(b => b.Name == "SMISKI Series1"))
        {
            _logger.Info("[SmiskiBlindBoxes] Blind box 'SMISKI Series1' đã tồn tại. Bỏ qua seed.");
            return;
        }

        // Tạo mới 6 sản phẩm cho blind box, ProductSaleType là BlindBoxOnly
        var blindBoxProducts = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "SMISKI Series1 - Smiski Hugging Knees",
                Description =
                    "Luôn ẩn mình trong một góc nhỏ, SMISKI lặng lẽ ôm lấy đầu gối, đôi mắt mơ màng nhìn xa xăm, chìm đắm trong những suy tư miên man. Hình ảnh này gợi lên một cảm giác yên bình, một chút cô đơn nhưng đầy chất thơ, như một người bạn nhỏ đang cùng bạn chia sẻ những khoảnh khắc tĩnh lặng của tâm hồn.",
                CategoryId = smiskiCategory.Id,
                SellerId = seller.Id,
                Price = 280000, Stock = 50, Status = ProductStatus.Active, CreatedAt = now,
                ImageUrls = new List<string> { "https://smiski.com/wp-content/uploads/2016/03/s1_01.png" },
                Brand = seller.CompanyName, Material = "PVC", ProductType = ProductSaleType.BlindBoxOnly, Height = 10
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "SMISKI Series1 - Smiski Sitting",
                Description =
                    "Bạn sẽ thường bắt gặp SMISKI này ngồi một mình trong im lặng, ánh mắt tập trung nhìn xuống một vật gì đó vô định. Với tính cách có phần cảnh giác và hướng nội, nó yêu thích không gian riêng tư, nơi nó có thể chìm vào thế giới của riêng mình mà không bị làm phiền. Một hình ảnh đầy bí ẩn và cuốn hút, dành cho những ai trân trọng khoảnh khắc được ở một mình.",
                CategoryId = smiskiCategory.Id,
                SellerId = seller.Id,
                Price = 280000, Stock = 50, Status = ProductStatus.Active, CreatedAt = now,
                ImageUrls = new List<string> { "https://smiski.com/wp-content/uploads/2016/03/s1_02.png" },
                Brand = seller.CompanyName, Material = "PVC", ProductType = ProductSaleType.BlindBoxOnly, Height = 10
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "SMISKI Series1 - Smiski Looking Back",
                Description =
                    "Đây là một SMISKI nhút nhát và dễ giật mình. Mỗi khi bạn vô tình phát hiện ra, nó sẽ lập tức quay lại, đôi mắt mở to nhìn bạn đầy ngạc nhiên và có chút bối rối. Khoảnh khắc đáng yêu này sẽ khiến bạn bất giác mỉm cười, như vừa khám phá ra một bí mật nhỏ bé và ngộ nghĩnh trong căn phòng của mình.",
                CategoryId = smiskiCategory.Id,
                SellerId = seller.Id,
                Price = 280000, Stock = 50, Status = ProductStatus.Active, CreatedAt = now,
                ImageUrls = new List<string> { "https://smiski.com/wp-content/uploads/2016/03/s1_03.png" },
                Brand = seller.CompanyName, Material = "PVC", ProductType = ProductSaleType.BlindBoxOnly, Height = 10
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "SMISKI Series1 - Smiski Lounging",
                Description =
                    "Gặp gỡ SMISKI lười biếng nhất hội! Nó chỉ thích nằm dài thư giãn ở bất cứ đâu, từ trên bàn làm việc cho đến kệ sách. Với nó, không có gì tuyệt vời hơn là được nghỉ ngơi và chẳng phải làm gì cả. Mọi hoạt động liên quan đến di chuyển hay vận động đều nằm ngoài từ điển của SMISKI này. Hãy để nó mang lại cho bạn một lời nhắc nhở nhẹ nhàng về việc tận hưởng những phút giây không làm gì.",
                CategoryId = smiskiCategory.Id,
                SellerId = seller.Id,
                Price = 280000, Stock = 50, Status = ProductStatus.Active, CreatedAt = now,
                ImageUrls = new List<string> { "https://smiski.com/wp-content/uploads/2016/03/s1_04.png" },
                Brand = seller.CompanyName, Material = "PVC", ProductType = ProductSaleType.BlindBoxOnly, Height = 10
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "SMISKI Series1 - Smiski Hiding",
                Description =
                    "Trốn tìm chính là biệt tài của SMISKI này. Nó là một bậc thầy trong việc tìm kiếm những nơi ẩn náu độc đáo, từ kẽ hở nhỏ nhất cho đến phía sau một chậu cây. Nó cảm thấy an toàn và thoải mái nhất khi được cuộn mình trong những không gian nhỏ hẹp. Sở hữu SMISKI này, bạn sẽ có một trò chơi trốn tìm nho nhỏ mỗi ngày trong chính căn phòng của mình.",
                CategoryId = smiskiCategory.Id,
                SellerId = seller.Id,
                Price = 280000, Stock = 50, Status = ProductStatus.Active, CreatedAt = now,
                ImageUrls = new List<string> { "https://smiski.com/wp-content/uploads/2016/03/s1_05.png" },
                Brand = seller.CompanyName, Material = "PVC", ProductType = ProductSaleType.BlindBoxOnly, Height = 10
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "SMISKI Series1 - Smiski Peeking",
                Description =
                    "Luôn trong tư thế khom người, SMISKI này rụt rè hé nhìn mọi thứ từ một góc an toàn. Nó có vẻ tò mò về thế giới xung quanh nhưng lại cần rất nhiều can đảm để thực sự tiến lại gần. Một hình ảnh vừa ngộ nghĩnh vừa đáng yêu, thể hiện sự tò mò nhưng đầy e dè, như một đứa trẻ đang khám phá thế giới rộng lớn.",
                CategoryId = smiskiCategory.Id,
                SellerId = seller.Id,
                Price = 500000, Stock = 20, Status = ProductStatus.Active, CreatedAt = now,
                ImageUrls = new List<string> { "https://smiski.com/wp-content/uploads/2016/03/s1_06.png" },
                Brand = seller.CompanyName, Material = "PVC", ProductType = ProductSaleType.BlindBoxOnly, Height = 10
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

        var totalWeightQty = rarityArr.Sum(x => x.Quantity * x.Weight);

        var blindBox = new BlindBox
        {
            Id = Guid.NewGuid(),
            SellerId = seller.Id,
            CategoryId = smiskiCategory.Id,
            Name = "SMISKI Series1",
            Description = "Blind box SMISKI Series1, a collection of small creatures that love hiding in corners.",
            Price = 350000,
            TotalQuantity = 30,
            HasSecretItem = true,
            SecretProbability = 5,
            Status = BlindBoxStatus.Approved,
            ImageUrl = "https://smiski.com/wp-content/uploads/2016/03/series1_img.png",
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

        _logger.Success("[SmiskiBlindBoxes] Đã seed blind box cho SMISKI Series1 thành công.");
    }

    private async Task SeedHighSecretBlindBoxes()
    {
        // Check if high secret blind boxes already exist
        var existingHighSecretBoxes = await _context.BlindBoxes
            .Where(b => b.SecretProbability == 25 && b.Status == BlindBoxStatus.Approved && !b.IsDeleted)
            .ToListAsync();

        if (existingHighSecretBoxes.Count >= 2)
        {
            _logger.Info("High secret blind boxes already exist, skipping creation.");
            return;
        }

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
            _logger.Warn("[SeedHighSecretBlindBoxes] Không tìm thấy category con để tạo blind box.");
            return;
        }

        foreach (var category in categories.Take(2)) // Tạo 2 high secret box từ 2 category đầu tiên
        {
            // Tạo mới 6 sản phẩm cho mỗi category, ProductSaleType là BlindBoxOnly
            var blindBoxProducts = new List<Product>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"HACIPUPU Super Rare Series Figure 1 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox với tỉ lệ secret cao.",
                    CategoryId = category.Id,
                    SellerId = seller.Id,
                    Price = 450000,
                    Stock = 20,
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
                    Name = $"HACIPUPU Super Rare Series Figure 2 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox với tỉ lệ secret cao.",
                    CategoryId = category.Id,
                    SellerId = seller.Id,
                    Price = 450000,
                    Stock = 20,
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
                    Name = $"HACIPUPU Super Rare Series Figure 3 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox với tỉ lệ secret cao.",
                    CategoryId = category.Id,
                    SellerId = seller.Id,
                    Price = 450000,
                    Stock = 20,
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
                    Name = $"HACIPUPU Super Rare Series Figure 4 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox với tỉ lệ secret cao.",
                    CategoryId = category.Id,
                    SellerId = seller.Id,
                    Price = 450000,
                    Stock = 20,
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
                    Name = $"HACIPUPU Super Rare Series Figure 5 ({category.Name})",
                    Description = "Mô hình đặc biệt dành cho blindbox với tỉ lệ secret cao.",
                    CategoryId = category.Id,
                    SellerId = seller.Id,
                    Price = 450000,
                    Stock = 20,
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
                    Name = $"HACIPUPU Super Rare Series SECRET ({category.Name})",
                    Description = "Mô hình SECRET đặc biệt dành cho blindbox với tỉ lệ cao.",
                    CategoryId = category.Id,
                    SellerId = seller.Id,
                    Price = 900000,
                    Stock = 10,
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

            // Rarity cấu hình với tỉ lệ SECRET cao (25%)
            var rarityArr = new[]
            {
                new { Rarity = RarityName.Common, Weight = 30, Quantity = 6 },
                new { Rarity = RarityName.Rare, Weight = 20, Quantity = 5 },
                new { Rarity = RarityName.Rare, Weight = 10, Quantity = 5 },
                new { Rarity = RarityName.Epic, Weight = 10, Quantity = 4 },
                new { Rarity = RarityName.Epic, Weight = 5, Quantity = 3 },
                new { Rarity = RarityName.Secret, Weight = 25, Quantity = 5 } // Tăng weight và quantity cho SECRET
            };

            // Tổng quantity * weight để tính drop rate
            var totalWeightQty = rarityArr.Sum(x => x.Quantity * x.Weight);

            var blindBox = new BlindBox
            {
                Id = Guid.NewGuid(),
                SellerId = seller.Id,
                CategoryId = category.Id,
                Name = $"Super Secret Blind Box - {category.Name}",
                Description = $"Blindbox đặc biệt với tỉ lệ SECRET cao 25% thuộc {category.Name}",
                Price = 800000, // Giá cao hơn do tỉ lệ SECRET cao
                TotalQuantity = 20,
                HasSecretItem = true,
                SecretProbability = 25, // Tỉ lệ SECRET cao 25%
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
                    ApprovedBy = sellerUser.Id,
                    ApprovedAt = now,
                    CreatedAt = now
                };
                await _context.ProbabilityConfigs.AddAsync(probCfg);
            }

            await _context.SaveChangesAsync();

            _logger.Success(
                $"[SeedHighSecretBlindBoxes] Đã seed high secret blind box cho category {category.Name} thành công.");
        }
    }

    private async Task SeedPromotions()
    {
        if (_context.Promotions.Any()) return;

        var now = DateTime.UtcNow;

        // Lấy admin/staff user để tạo global promotion
        var adminUser = await _context.Users
            .FirstOrDefaultAsync(u => u.RoleName == RoleType.Staff || u.RoleName == RoleType.Admin);

        // Lấy seller BlindTreasure
        var blindTreasureSeller = await _context.Sellers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.User.Email == "blindtreasurefpt@gmail.com");

        if (blindTreasureSeller == null || adminUser == null) return;

        var promotions = new List<Promotion>
        {
            // 1. Voucher toàn sàn (do admin/staff tạo) - Theo logic Admin/Staff
            new()
            {
                Id = Guid.NewGuid(),
                Code = "GLOBAL5",
                Description = "Giảm 5% toàn sàn - Khuyến mãi từ BlindTreasure",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 5,
                StartDate = now, // ✅ Bắt đầu ngay
                EndDate = now.AddMonths(1),
                UsageLimit = 1000,
                Status = PromotionStatus.Approved, // ✅ Admin/Staff tự động approved
                SellerId = null, // ✅ Global promotion
                CreatedByRole = RoleType.Staff,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            },

            // 2. Voucher của BlindTreasure seller - Theo logic Seller  
            new()
            {
                Id = Guid.NewGuid(),
                Code = "BLINDT10",
                Description = "Giảm 10% sản phẩm BlindTreasure - Ưu đãi đặc biệt",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 10,
                StartDate = now, // ✅ Bắt đầu ngay
                EndDate = now.AddMonths(1),
                UsageLimit = 100,
                Status = PromotionStatus.Approved, // ✅ Giả sử đã được duyệt
                SellerId = blindTreasureSeller.Id, // ✅ Seller-specific promotion
                CreatedByRole = RoleType.Seller,
                CreatedBy = blindTreasureSeller.UserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            },

            // 3. Thêm 1 voucher Fixed Amount để test
            new()
            {
                Id = Guid.NewGuid(),
                Code = "SAVE20K",
                Description = "Giảm 20,000 VNĐ cho đơn hàng - Toàn sàn",
                DiscountType = DiscountType.Fixed,
                DiscountValue = 20000,
                StartDate = now, // ✅ Bắt đầu ngay
                EndDate = now.AddMonths(2),
                UsageLimit = 500,
                Status = PromotionStatus.Approved,
                SellerId = null, // Global
                CreatedByRole = RoleType.Staff,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            }
        };

        await _context.Promotions.AddRangeAsync(promotions);
        await _context.SaveChangesAsync();
    }

    private async Task SeedPromotionParticipants()
    {
        if (_context.PromotionParticipants.Any()) return;

        var now = DateTime.UtcNow;

        // Lấy seller BlindTreasure
        var blindTreasureSeller = await _context.Sellers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.User.Email == "blindtreasurefpt@gmail.com");

        if (blindTreasureSeller == null) return;

        // ✅ THEO LOGIC SERVICE: Seller tự tạo promotion sẽ tự động có PromotionParticipant
        var sellerPromotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.SellerId == blindTreasureSeller.Id && p.Code == "BLINDT10");

        // ✅ THEO LOGIC SERVICE: Lấy các global promotions (đã bắt đầu rồi)
        var globalPromotions = await _context.Promotions
            .Where(p => p.SellerId == null &&
                        p.Status == PromotionStatus.Approved)
            .ToListAsync();

        var participants = new List<PromotionParticipant>();

        // 1. ✅ Tự động tạo participant cho promotion của chính seller
        if (sellerPromotion != null)
        {
            participants.Add(new PromotionParticipant
            {
                Id = Guid.NewGuid(),
                PromotionId = sellerPromotion.Id,
                SellerId = blindTreasureSeller.Id,
                JoinedAt = now, // ✅ Join ngay khi tạo promotion
                CreatedBy = blindTreasureSeller.UserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            });
        }

        // 2. ✅ BlindTreasure tham gia các global promotions (tối đa 2)
        // ⚠️ LUU Ý: Vì promotions đã bắt đầu rồi, nên theo business rule
        // sẽ không thể join được nữa. Nhưng để test, ta vẫn tạo data.
        var joinCount = 0;
        foreach (var globalPromotion in globalPromotions.Take(2)) // Tối đa 2 global promotions
        {
            participants.Add(new PromotionParticipant
            {
                Id = Guid.NewGuid(),
                PromotionId = globalPromotion.Id,
                SellerId = blindTreasureSeller.Id,
                JoinedAt = now.AddMinutes(-joinCount * 5), // ✅ Join trước khi promotion bắt đầu (để hợp lý)
                CreatedBy = blindTreasureSeller.UserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            });
            joinCount++;
        }

        if (participants.Any())
        {
            await _context.PromotionParticipants.AddRangeAsync(participants);
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedSellerForUser(string sellerEmail)
    {
        var sellerUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == sellerEmail);
        if (sellerUser == null)
        {
            _logger.Error($"Không tìm thấy user với email {sellerEmail} để tạo Seller.");
            return;
        }

        Seller seller;

        switch (sellerEmail)
        {
            case "smiskiofficial@gmail.com":
                seller = new Seller
                {
                    UserId = sellerUser.Id,
                    IsVerified = true,
                    CoaDocumentUrl = "https://example.com/smiski-coa.pdf",
                    CompanyName = "Smiski Official Store Tokyo Japan",
                    TaxId = "123456789",
                    CompanyAddress = "336 Trường Sa, Phường 7, quận Phú Nhuận",
                    Status = SellerStatus.Approved,
                    CreatedAt = DateTime.UtcNow,
                    CompanyPhone = "0901234567",
                    CompanyWardName = "Phường 7",
                    CompanyDistrictName = "Quận Phú Nhuận",
                    CompanyProvinceName = "HCM"
                };
                break;

            case "blindtreasurefpt@gmail.com":
            default:
                seller = new Seller
                {
                    UserId = sellerUser.Id,
                    IsVerified = true,
                    CoaDocumentUrl = "https://example.com/coa.pdf",
                    CompanyName = "Blind Treasure Ltd.",
                    TaxId = "987654321",
                    CompanyAddress = "72 Thành Thái, Phường 12, Quận 10, Hồ Chí Minh, Vietnam",
                    Status = SellerStatus.Approved,
                    CreatedAt = DateTime.UtcNow,
                    CompanyPhone = "0325134357",
                    CompanyWardName = "Phường 12",
                    CompanyDistrictName = "Quận 10",
                    CompanyProvinceName = "HCM"
                };
                break;
        }

        await _context.Sellers.AddAsync(seller);
        await _context.SaveChangesAsync();
        _logger.Info($"Seller seeded successfully for {sellerEmail}.");
    }

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

    #endregion
}

public class MockClaimsService : IClaimsService
{
    private readonly Guid _userId;

    public MockClaimsService(Guid userId)
    {
        _userId = userId;
    }

    public Guid CurrentUserId => _userId;
    public string? IpAddress => "127.0.0.1";
}