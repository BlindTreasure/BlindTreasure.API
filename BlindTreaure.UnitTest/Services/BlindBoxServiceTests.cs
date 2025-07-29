using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MockQueryable.Moq;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class BlindBoxServiceTests
{
    private readonly Mock<IGenericRepository<BlindBoxItem>> _blindBoxItemRepoMock;

    // Repository mocks
    private readonly Mock<IGenericRepository<BlindBox>> _blindBoxRepoMock;
    private readonly BlindBoxService _blindBoxService;
    private readonly Mock<IBlobService> _blobServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IGenericRepository<Category>> _categoryRepoMock;
    private readonly Mock<ICategoryService> _categoryServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<IEmailService> _emailServiceMock;

    private readonly DateTime _fixedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IMapperService> _mapperServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IGenericRepository<ProbabilityConfig>> _probabilityConfigRepoMock;
    private readonly Mock<IGenericRepository<Product>> _productRepoMock;
    private readonly Mock<IGenericRepository<RarityConfig>> _rarityConfigRepoMock;
    private readonly Guid _sellerId = Guid.NewGuid();
    private readonly Mock<IGenericRepository<Seller>> _sellerRepoMock;
    private readonly Mock<ICurrentTime> _timeMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<User>> _userRepoMock;

    public BlindBoxServiceTests()
    {
        // Initialize mocks
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _timeMock = new Mock<ICurrentTime>();
        _mapperServiceMock = new Mock<IMapperService>();
        _blobServiceMock = new Mock<IBlobService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _emailServiceMock = new Mock<IEmailService>();
        _categoryServiceMock = new Mock<ICategoryService>();
        _notificationServiceMock = new Mock<INotificationService>();

        // Repository mocks
        _blindBoxRepoMock = new Mock<IGenericRepository<BlindBox>>();
        _blindBoxItemRepoMock = new Mock<IGenericRepository<BlindBoxItem>>();
        _sellerRepoMock = new Mock<IGenericRepository<Seller>>();
        _productRepoMock = new Mock<IGenericRepository<Product>>();
        _rarityConfigRepoMock = new Mock<IGenericRepository<RarityConfig>>();
        _probabilityConfigRepoMock = new Mock<IGenericRepository<ProbabilityConfig>>();
        _userRepoMock = new Mock<IGenericRepository<User>>();
        _categoryRepoMock = new Mock<IGenericRepository<Category>>();

        // Setup UnitOfWork
        _unitOfWorkMock.Setup(x => x.BlindBoxes).Returns(_blindBoxRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.BlindBoxItems).Returns(_blindBoxItemRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Sellers).Returns(_sellerRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Products).Returns(_productRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.RarityConfigs).Returns(_rarityConfigRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.ProbabilityConfigs).Returns(_probabilityConfigRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepoMock.Object);

        // Setup basic services
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(_currentUserId);
        _timeMock.Setup(x => x.GetCurrentTime()).Returns(_fixedTime);

        _blindBoxService = new BlindBoxService(
            _unitOfWorkMock.Object,
            _claimsServiceMock.Object,
            _timeMock.Object,
            _mapperServiceMock.Object,
            _blobServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerServiceMock.Object,
            _emailServiceMock.Object,
            _categoryServiceMock.Object,
            _notificationServiceMock.Object
        );
    }

    #region GetBlindBoxByIdAsync Tests

    [Fact]
    public async Task GetBlindBoxByIdAsync_ShouldReturnFromCache_WhenCacheExists()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var cachedResult = new BlindBoxDetailDto
        {
            Id = blindBoxId,
            Name = "Cached BlindBox"
        };

        _cacheServiceMock.Setup(x => x.GetAsync<BlindBoxDetailDto>(It.IsAny<string>()))
            .ReturnsAsync(cachedResult);

        // Act
        var result = await _blindBoxService.GetBlindBoxByIdAsync(blindBoxId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(blindBoxId);
        result.Name.Should().Be("Cached BlindBox");
        _blindBoxRepoMock.Verify(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task GetBlindBoxByIdAsync_ShouldReturnFromDatabase_WhenCacheNotExists()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var blindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Test BlindBox",
            Description = "Test Description",
            SellerId = _sellerId,
            CategoryId = Guid.NewGuid(),
            Price = 100,
            TotalQuantity = 50,
            Status = BlindBoxStatus.Draft,
            ReleaseDate = _fixedTime,
            IsDeleted = false
        };

        var blindBoxItems = new List<BlindBoxItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                BlindBoxId = blindBoxId,
                ProductId = Guid.NewGuid(),
                Quantity = 10,
                DropRate = 50m,
                IsDeleted = false,
                Product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    SellerId = _sellerId,
                    CategoryId = Guid.NewGuid(),
                    Price = 50,
                    Stock = 100,
                    Status = ProductStatus.Active,
                    ProductType = ProductSaleType.BlindBoxOnly,
                    Description = "Test Product Description"
                },
                RarityConfig = new RarityConfig
                {
                    Id = Guid.NewGuid(),
                    Name = RarityName.Common,
                    Weight = 50,
                    IsSecret = false
                }
            }
        };

        _cacheServiceMock.Setup(x => x.GetAsync<BlindBoxDetailDto>(It.IsAny<string>()))
            .ReturnsAsync((BlindBoxDetailDto)null!);

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()))
            .ReturnsAsync(blindBox);

        // Mock GetQueryable with MockQueryable
        var mockQueryable = blindBoxItems.AsQueryable().BuildMock();
        _blindBoxItemRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _mapperServiceMock.Setup(x => x.Map<BlindBox, BlindBoxDetailDto>(It.IsAny<BlindBox>()))
            .Returns(new BlindBoxDetailDto { Id = blindBoxId, Name = "Test BlindBox" });

        _cacheServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<BlindBoxDetailDto>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _blindBoxService.GetBlindBoxByIdAsync(blindBoxId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(blindBoxId);
        _blindBoxRepoMock.Verify(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()), Times.Once);
        _cacheServiceMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<BlindBoxDetailDto>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    #endregion

    #region CreateBlindBoxAsync Tests

    [Fact]
    public async Task CreateBlindBoxAsync_ShouldCreateBlindBox_WhenValidData()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var dto = new CreateBlindBoxDto
        {
            Name = "Test Blind Box",
            Price = 100,
            TotalQuantity = 50,
            Description = "Test Description",
            CategoryId = categoryId,
            ReleaseDate = _fixedTime.AddDays(7),
            ImageFile = CreateMockFormFile()
        };

        var seller = new Seller
        {
            Id = _sellerId,
            UserId = _currentUserId,
            Status = SellerStatus.Approved,
            IsDeleted = false
        };

        var category = new Category { Id = categoryId };
        var createdBlindBox = new BlindBox
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            SellerId = _sellerId,
            CategoryId = categoryId,
            Price = dto.Price,
            TotalQuantity = dto.TotalQuantity,
            Status = BlindBoxStatus.Draft,
            ReleaseDate = _fixedTime.AddDays(7),
            IsDeleted = false
        };

        // Setup mocks
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);

        _categoryServiceMock.Setup(x => x.GetWithParentAsync(categoryId))
            .ReturnsAsync(category);

        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<Category>().AsQueryable().BuildMock());

        _blobServiceMock.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);

        _blobServiceMock.Setup(x => x.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://example.com/image.jpg");

        _blindBoxRepoMock.Setup(x => x.AddAsync(It.IsAny<BlindBox>()))
            .ReturnsAsync(createdBlindBox);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock recursive call to GetBlindBoxByIdAsync
        _cacheServiceMock.SetupSequence(x => x.GetAsync<BlindBoxDetailDto>(It.IsAny<string>()))
            .ReturnsAsync((BlindBoxDetailDto)null!) // First call in CreateBlindBoxAsync
            .ReturnsAsync((BlindBoxDetailDto)null!); // Second call in GetBlindBoxByIdAsync

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()))
            .ReturnsAsync(createdBlindBox);

        _blindBoxItemRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<BlindBoxItem>().AsQueryable().BuildMock());

        _mapperServiceMock.Setup(x => x.Map<BlindBox, BlindBoxDetailDto>(It.IsAny<BlindBox>()))
            .Returns(new BlindBoxDetailDto { Id = createdBlindBox.Id, Name = dto.Name });

        _cacheServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<BlindBoxDetailDto>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _blindBoxService.CreateBlindBoxAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(dto.Name);
        _blindBoxRepoMock.Verify(x => x.AddAsync(It.IsAny<BlindBox>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateBlindBoxAsync_ShouldThrowException_WhenDataIsNull()
    {
        // Act & Assert
        var act = async () => await _blindBoxService.CreateBlindBoxAsync(null!);

        await act.Should().ThrowAsync<Exception>();
        // Không check status code cụ thể vì null check có thể throw NullReferenceException (500)
    }

    #region Helper Methods

    private static IFormFile CreateMockFormFile()
    {
        var content = "Hello World from a Fake File"u8.ToArray();
        var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, stream.Length, "Data", "dummy.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        return file;
    }

    #endregion

    #endregion

    #region UpdateBlindBoxAsync Tests

    [Fact]
    public async Task UpdateBlindBoxAsync_ShouldUpdateBlindBox_WhenValidData()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var existingBlindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Original Name",
            Description = "Original Description",
            SellerId = _sellerId,
            CategoryId = Guid.NewGuid(),
            Price = 100,
            TotalQuantity = 50,
            Status = BlindBoxStatus.Draft,
            IsDeleted = false
        };

        var seller = new Seller
        {
            Id = _sellerId,
            UserId = _currentUserId,
            Status = SellerStatus.Approved,
            IsDeleted = false
        };

        var dto = new UpdateBlindBoxDto
        {
            Name = "Updated Name",
            Description = "Updated Description",
            Price = 150,
            TotalQuantity = 75,
            CategoryId = categoryId
        };

        var category = new Category { Id = categoryId };

        // Setup mocks
        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()))
            .ReturnsAsync(existingBlindBox);

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);

        _categoryServiceMock.Setup(x => x.GetWithParentAsync(categoryId))
            .ReturnsAsync(category);

        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<Category>().AsQueryable().BuildMock());

        _blindBoxRepoMock.Setup(x => x.Update(It.IsAny<BlindBox>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetBlindBoxByIdAsync call at the end
        _cacheServiceMock.Setup(x => x.GetAsync<BlindBoxDetailDto>(It.IsAny<string>()))
            .ReturnsAsync((BlindBoxDetailDto)null!);

        _blindBoxItemRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<BlindBoxItem>().AsQueryable().BuildMock());

        _mapperServiceMock.Setup(x => x.Map<BlindBox, BlindBoxDetailDto>(It.IsAny<BlindBox>()))
            .Returns(new BlindBoxDetailDto { Id = blindBoxId, Name = dto.Name });

        _cacheServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<BlindBoxDetailDto>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _blindBoxService.UpdateBlindBoxAsync(blindBoxId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(blindBoxId);
        result.Name.Should().Be(dto.Name);
        _blindBoxRepoMock.Verify(x => x.Update(It.IsAny<BlindBox>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateBlindBoxAsync_ShouldThrowNotFound_WhenBlindBoxNotExists()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var dto = new UpdateBlindBoxDto
        {
            Name = "Updated Name"
        };

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()))
            .ReturnsAsync((BlindBox)null!);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _blindBoxService.UpdateBlindBoxAsync(blindBoxId, dto));

        Assert.Equal(404, exception.Data["StatusCode"]);
    }

    [Fact]
    public async Task UpdateBlindBoxAsync_ShouldThrowForbidden_WhenSellerNotOwnsBlindBox()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var differentSellerId = Guid.NewGuid();
        var existingBlindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Original Name",
            Description = "Original Description",
            SellerId = differentSellerId, // Different seller
            IsDeleted = false
        };

        var dto = new UpdateBlindBoxDto
        {
            Name = "Updated Name"
        };

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()))
            .ReturnsAsync(existingBlindBox);

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync((Seller)null!); // Seller not found because it's different user

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _blindBoxService.UpdateBlindBoxAsync(blindBoxId, dto));

        Assert.Equal(403, exception.Data["StatusCode"]);
    }

    #endregion

    #region AddItemsToBlindBoxAsync Tests

    [Fact]
    public async Task AddItemsToBlindBoxAsync_ShouldAddItems_WhenValidData()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var productIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();

        var existingBlindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Test BlindBox",
            Description = "Test Description",
            SellerId = _sellerId,
            Status = BlindBoxStatus.Draft,
            IsDeleted = false,
            BlindBoxItems = new List<BlindBoxItem>()
        };

        var seller = new Seller
        {
            Id = _sellerId,
            UserId = _currentUserId,
            Status = SellerStatus.Approved,
            IsDeleted = false
        };

        // Tạo 6 items với 1 Secret và 5 Common, tổng weight = 100
        var items = new List<BlindBoxItemRequestDto>
        {
            new()
            {
                ProductId = productIds[0],
                Quantity = 10,
                Rarity = RarityName.Common,
                Weight = 20
            },
            new()
            {
                ProductId = productIds[1],
                Quantity = 10,
                Rarity = RarityName.Common,
                Weight = 20
            },
            new()
            {
                ProductId = productIds[2],
                Quantity = 10,
                Rarity = RarityName.Common,
                Weight = 20
            },
            new()
            {
                ProductId = productIds[3],
                Quantity = 10,
                Rarity = RarityName.Common,
                Weight = 20
            },
            new()
            {
                ProductId = productIds[4],
                Quantity = 10,
                Rarity = RarityName.Rare,
                Weight = 15
            },
            new()
            {
                ProductId = productIds[5],
                Quantity = 5,
                Rarity = RarityName.Secret, // Phải có ít nhất 1 Secret
                Weight = 5
            }
        };

        // Tạo products tương ứng
        var products = productIds.Select((id, index) => new Product
        {
            Id = id,
            Name = $"Product {index + 1}",
            SellerId = _sellerId,
            Stock = 100,
            Status = ProductStatus.Active,
            IsDeleted = false
        }).ToList();

        // Setup mocks
        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<BlindBox, bool>>>(),
                It.IsAny<Expression<Func<BlindBox, object>>[]>()))
            .ReturnsAsync(existingBlindBox);

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);

        _productRepoMock.Setup(x => x.GetAllAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Expression<Func<Product, object>>[]>()))
            .ReturnsAsync(products);

        _blindBoxItemRepoMock.Setup(x => x.AddRangeAsync(It.IsAny<List<BlindBoxItem>>()))
            .Returns(Task.CompletedTask);

        _rarityConfigRepoMock.Setup(x => x.AddRangeAsync(It.IsAny<List<RarityConfig>>()))
            .Returns(Task.CompletedTask);

        _blindBoxRepoMock.Setup(x => x.Update(It.IsAny<BlindBox>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetBlindBoxByIdAsync call at the end
        _cacheServiceMock.Setup(x => x.GetAsync<BlindBoxDetailDto>(It.IsAny<string>()))
            .ReturnsAsync((BlindBoxDetailDto)null!);

        _blindBoxItemRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<BlindBoxItem>().AsQueryable().BuildMock());

        _mapperServiceMock.Setup(x => x.Map<BlindBox, BlindBoxDetailDto>(It.IsAny<BlindBox>()))
            .Returns(new BlindBoxDetailDto { Id = blindBoxId, Name = "Test BlindBox" });

        _cacheServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<BlindBoxDetailDto>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _blindBoxService.AddItemsToBlindBoxAsync(blindBoxId, items);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(blindBoxId, result.Id);
        _blindBoxItemRepoMock.Verify(x => x.AddRangeAsync(It.IsAny<List<BlindBoxItem>>()), Times.Once);
        _rarityConfigRepoMock.Verify(x => x.AddRangeAsync(It.IsAny<List<RarityConfig>>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AddItemsToBlindBoxAsync_ShouldThrowBadRequest_WhenInvalidItemCount()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var items = new List<BlindBoxItemRequestDto>
        {
            new()
            {
                ProductId = Guid.NewGuid(),
                Quantity = 10,
                Rarity = RarityName.Common,
                Weight = 100
            }
            // Only 1 item - should be 6 or 12
        };

        // Act & Assert
        var act = async () => await _blindBoxService.AddItemsToBlindBoxAsync(blindBoxId, items);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Data["StatusCode"].Equals(400) &&
                        e.Message.Contains("6 hoặc 12 sản phẩm"));
    }

    [Fact]
    public async Task AddItemsToBlindBoxAsync_ShouldThrowBadRequest_WhenNoSecretItem()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var items = new List<BlindBoxItemRequestDto>();

        // Create 6 items but no Secret rarity
        for (var i = 0; i < 6; i++)
            items.Add(new BlindBoxItemRequestDto
            {
                ProductId = Guid.NewGuid(),
                Quantity = 10,
                Rarity = RarityName.Common, // All Common, no Secret
                Weight = 100 / 6 // Equal weight distribution
            });

        var existingBlindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Test BlindBox",
            Description = "Test Description",
            SellerId = _sellerId,
            IsDeleted = false,
            BlindBoxItems = new List<BlindBoxItem>()
        };

        var seller = new Seller
        {
            Id = _sellerId,
            UserId = _currentUserId,
            Status = SellerStatus.Approved,
            IsDeleted = false
        };

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<BlindBox, bool>>>(),
                It.IsAny<Expression<Func<BlindBox, object>>[]>()))
            .ReturnsAsync(existingBlindBox);

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);

        // Act & Assert
        var act = async () => await _blindBoxService.AddItemsToBlindBoxAsync(blindBoxId, items);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Data["StatusCode"].Equals(400) &&
                        e.Message.Contains("ít nhất 1 item Secret"));
    }

    [Fact]
    public async Task AddItemsToBlindBoxAsync_ShouldThrowForbidden_WhenSellerNotOwnsBlindBox()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var differentSellerId = Guid.NewGuid();

        // Tạo 6 items hợp lệ để vượt qua validation đầu tiên
        var items = new List<BlindBoxItemRequestDto>();
        for (var i = 0; i < 6; i++)
            items.Add(new BlindBoxItemRequestDto
            {
                ProductId = Guid.NewGuid(),
                Quantity = 10,
                Rarity = i == 5 ? RarityName.Secret : RarityName.Common, // Item cuối là Secret
                Weight = i == 5 ? 10 : 18 // Tổng = 18*5 + 10 = 100
            });

        var existingBlindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Test BlindBox",
            Description = "Test Description",
            SellerId = differentSellerId, // Different seller
            IsDeleted = false,
            BlindBoxItems = new List<BlindBoxItem>()
        };

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<BlindBox, bool>>>(),
                It.IsAny<Expression<Func<BlindBox, object>>[]>()))
            .ReturnsAsync(existingBlindBox);

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync((Seller)null!); // Seller not found

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _blindBoxService.AddItemsToBlindBoxAsync(blindBoxId, items));

        // Sử dụng ExceptionUtils nếu muốn
        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        Assert.Equal(403, statusCode);
    }

    #endregion

    #region SubmitBlindBoxAsync Tests

    [Fact]
    public async Task SubmitBlindBoxAsync_ShouldSubmitBlindBox_WhenBlindBoxHasValidItems()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var blindBoxId = Guid.NewGuid();

        // Tạo product trước và đảm bảo ID giống nhau
        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            SellerId = _sellerId,
            Stock = 100,
            Status = ProductStatus.Active
        };

        var existingBlindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Test BlindBox",
            Description = "Test Description",
            SellerId = _sellerId,
            Status = BlindBoxStatus.Draft,
            IsDeleted = false,
            BlindBoxItems = new List<BlindBoxItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BlindBoxId = blindBoxId,
                    ProductId = productId, // Sử dụng ID giống với product đã tạo
                    Quantity = 10,
                    DropRate = 50m,
                    IsDeleted = false
                }
            }
        };

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<BlindBox, bool>>>(),
                It.IsAny<Expression<Func<BlindBox, object>>[]>()))
            .ReturnsAsync(existingBlindBox);

        // Thiết lập mock để trả về đúng sản phẩm với ID trùng khớp
        _productRepoMock.Setup(x => x.GetAllAsync(
                It.Is<Expression<Func<Product, bool>>>(expr => true),
                It.IsAny<Expression<Func<Product, object>>[]>()))
            .ReturnsAsync(new List<Product> { product });

        _blindBoxRepoMock.Setup(x => x.Update(It.IsAny<BlindBox>()))
            .ReturnsAsync(true);

        _productRepoMock.Setup(x => x.UpdateRange(It.IsAny<List<Product>>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetBlindBoxByIdAsync call at the end
        _cacheServiceMock.Setup(x => x.GetAsync<BlindBoxDetailDto>(It.IsAny<string>()))
            .ReturnsAsync((BlindBoxDetailDto)null!);

        _blindBoxItemRepoMock.Setup(x => x.GetQueryable())
            .Returns(existingBlindBox.BlindBoxItems.AsQueryable().BuildMock());

        _mapperServiceMock.Setup(x => x.Map<BlindBox, BlindBoxDetailDto>(It.IsAny<BlindBox>()))
            .Returns(new BlindBoxDetailDto { Id = blindBoxId, Name = "Test BlindBox" });

        _cacheServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<BlindBoxDetailDto>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _blindBoxService.SubmitBlindBoxAsync(blindBoxId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(blindBoxId);
        _blindBoxRepoMock.Verify(x => x.Update(It.IsAny<BlindBox>()), Times.Once);
        _productRepoMock.Verify(x => x.UpdateRange(It.IsAny<List<Product>>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SubmitBlindBoxAsync_ShouldThrowBadRequest_WhenProductHasInsufficientStock()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var blindBoxId = Guid.NewGuid();

        // Tạo product với ID cụ thể và stock thấp
        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            SellerId = _sellerId,
            Stock = 20, // Stock không đủ
            Status = ProductStatus.Active
        };

        var existingBlindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "Test BlindBox",
            Description = "Test Description",
            SellerId = _sellerId,
            Status = BlindBoxStatus.Draft,
            IsDeleted = false,
            BlindBoxItems = new List<BlindBoxItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BlindBoxId = blindBoxId,
                    ProductId = productId, // Sử dụng ID trùng với product đã tạo
                    Quantity = 50, // Yêu cầu nhiều hơn stock
                    DropRate = 50m,
                    IsDeleted = false
                }
            }
        };

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<BlindBox, bool>>>(),
                It.IsAny<Expression<Func<BlindBox, object>>[]>()))
            .ReturnsAsync(existingBlindBox);

        // Thiết lập mock để trả về đúng sản phẩm có ID trùng khớp
        _productRepoMock.Setup(x => x.GetAllAsync(
                It.Is<Expression<Func<Product, bool>>>(expr => true),
                It.IsAny<Expression<Func<Product, object>>[]>()))
            .ReturnsAsync(new List<Product> { product });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _blindBoxService.SubmitBlindBoxAsync(blindBoxId));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        Assert.Equal(400, statusCode);
        Assert.Contains("không đủ tồn kho", exception.Message.ToLower());
    }

    #endregion

    #region ClearItemsFromBlindBoxAsync Tests

    [Fact]
    public async Task ClearItemsFromBlindBoxAsync_ShouldClearItemsAndRestoreStock_WhenSellerOwnsBlindBox()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = _sellerId,
            UserId = _currentUserId,
            Status = SellerStatus.Approved,
            IsDeleted = false
        };
        var product = new Product
        {
            Id = productId,
            SellerId = _sellerId,
            Stock = 10,
            IsDeleted = false
        };
        var blindBoxItem = new BlindBoxItem
        {
            Id = Guid.NewGuid(),
            BlindBoxId = blindBoxId,
            ProductId = productId,
            Quantity = 5,
            IsDeleted = false
        };
        var blindBox = new BlindBox
        {
            Id = blindBoxId,
            Name = "skibidi",
            Description = "skibidi",
            SellerId = _sellerId,
            BlindBoxItems = new List<BlindBoxItem> { blindBoxItem },
            IsDeleted = false
        };
        // Mock repo/service
        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<BlindBox, bool>>>(),
                It.IsAny<Expression<Func<BlindBox, object>>[]>()))
            .ReturnsAsync(blindBox);
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);
        _productRepoMock.Setup(x => x.GetAllAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Expression<Func<Product, object>>[]>()))
            .ReturnsAsync(new List<Product> { product });
        _productRepoMock.Setup(x => x.UpdateRange(It.IsAny<List<Product>>()))
            .ReturnsAsync(true);
        _blindBoxItemRepoMock.Setup(x => x.SoftRemoveRange(It.IsAny<List<BlindBoxItem>>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _blindBoxItemRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<BlindBoxItem>().AsQueryable().BuildMock());
        _mapperServiceMock.Setup(x => x.Map<BlindBox, BlindBoxDetailDto>(It.IsAny<BlindBox>()))
            .Returns(new BlindBoxDetailDto { Id = blindBoxId, Name = "Test BlindBox" });
        _cacheServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<BlindBoxDetailDto>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        // Act
        var result = await _blindBoxService.ClearItemsFromBlindBoxAsync(blindBoxId);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(blindBoxId, result.Id);
        _productRepoMock.Verify(x => x.UpdateRange(It.Is<List<Product>>(l => l[0].Stock == 15)), Times.Once);
        _blindBoxItemRepoMock.Verify(x => x.SoftRemoveRange(It.IsAny<List<BlindBoxItem>>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    #endregion

    #region DeleteBlindBoxAsync Tests

    [Fact]
    public async Task DeleteBlindBoxAsync_ShouldSoftDeleteBlindBoxAndRestoreStock_WhenSellerOwnsBlindBox()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = _sellerId,
            UserId = _currentUserId,
            Status = SellerStatus.Approved,
            IsDeleted = false
        };
        var product = new Product
        {
            Id = productId,
            SellerId = _sellerId,
            Stock = 10,
            IsDeleted = false
        };
        var blindBoxItem = new BlindBoxItem
        {
            Id = Guid.NewGuid(),
            BlindBoxId = blindBoxId,
            ProductId = productId,
            Quantity = 5,
            IsDeleted = false
        };
        var blindBox = new BlindBox
        {
            Id = blindBoxId,
            SellerId = _sellerId,
            Name = "skibidi",
            Description = "skibidi",
            BlindBoxItems = new List<BlindBoxItem> { blindBoxItem },
            IsDeleted = false
        };
        // Mock repo/service
        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<BlindBox, bool>>>(),
                It.IsAny<Expression<Func<BlindBox, object>>[]>()))
            .ReturnsAsync(blindBox);
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);
        _blindBoxRepoMock.Setup(x => x.SoftRemove(It.IsAny<BlindBox>()))
            .ReturnsAsync(true);
        _productRepoMock.Setup(x => x.GetAllAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Expression<Func<Product, object>>[]>()))
            .ReturnsAsync(new List<Product> { product });
        _productRepoMock.Setup(x => x.UpdateRange(It.IsAny<List<Product>>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _blindBoxItemRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<BlindBoxItem>().AsQueryable().BuildMock());
        _mapperServiceMock.Setup(x => x.Map<BlindBox, BlindBoxDetailDto>(It.IsAny<BlindBox>()))
            .Returns(new BlindBoxDetailDto { Id = blindBoxId, Name = "Test BlindBox" });
        // Act
        var result = await _blindBoxService.DeleteBlindBoxAsync(blindBoxId);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(blindBoxId, result.Id);
        _blindBoxRepoMock.Verify(x => x.SoftRemove(It.IsAny<BlindBox>()), Times.Once);
        _productRepoMock.Verify(x => x.UpdateRange(It.Is<List<Product>>(l => l[0].Stock == 15)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    #endregion
}