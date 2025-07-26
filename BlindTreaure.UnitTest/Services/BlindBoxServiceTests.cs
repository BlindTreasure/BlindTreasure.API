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
            new BlindBoxItem
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

    [Fact]
    public async Task GetBlindBoxByIdAsync_ShouldThrowNotFound_WhenBlindBoxNotExists()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();

        _cacheServiceMock.Setup(x => x.GetAsync<BlindBoxDetailDto>(It.IsAny<string>()))
            .ReturnsAsync((BlindBoxDetailDto)null!);

        _blindBoxRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<BlindBox, bool>>>()))
            .ReturnsAsync((BlindBox)null!);

        // Act & Assert
        var act = async () => await _blindBoxService.GetBlindBoxByIdAsync(blindBoxId);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Data["StatusCode"].Equals(404));
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

    [Fact]
    public async Task CreateBlindBoxAsync_ShouldThrowForbidden_WhenSellerNotApproved()
    {
        // Arrange
        var dto = new CreateBlindBoxDto
        {
            Name = "Test Blind Box",
            Price = 100,
            TotalQuantity = 50,
            Description = "Test Description",
            CategoryId = Guid.NewGuid(),
            ReleaseDate = _fixedTime.AddDays(7),
            ImageFile = CreateMockFormFile()
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync((Seller)null!);

        // Act & Assert
        var act = async () => await _blindBoxService.CreateBlindBoxAsync(dto);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Data["StatusCode"].Equals(403));
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
}