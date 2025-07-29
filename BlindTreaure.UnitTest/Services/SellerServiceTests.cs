using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class SellerServiceTests
{
    private readonly SellerService _sellerService;
    private readonly Mock<IBlobService> _blobServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IMapperService> _mapperServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IProductService> _productServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<Seller>> _sellerRepoMock;
    private readonly Mock<IGenericRepository<User>> _userRepoMock;
    private readonly Mock<IGenericRepository<Product>> _productRepoMock;

    public SellerServiceTests()
    {
        _blobServiceMock = new Mock<IBlobService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _mapperServiceMock = new Mock<IMapperService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _productServiceMock = new Mock<IProductService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sellerRepoMock = new Mock<IGenericRepository<Seller>>();
        _userRepoMock = new Mock<IGenericRepository<User>>();
        _productRepoMock = new Mock<IGenericRepository<Product>>();

        _unitOfWorkMock.Setup(x => x.Sellers).Returns(_sellerRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Products).Returns(_productRepoMock.Object);

        _sellerService = new SellerService(
            _blobServiceMock.Object,
            _emailServiceMock.Object,
            _loggerServiceMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _mapperServiceMock.Object,
            _claimsServiceMock.Object,
            _productServiceMock.Object,
            _notificationServiceMock.Object
        );
    }

    #region UpdateSellerInfoAsync Tests

    [Fact]
    public async Task UpdateSellerInfoAsync_ShouldUpdateSellerInfo_WhenValidData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User
            {
                Email = "hehe@gmail.com",
                RoleName = RoleType.Seller,
                Id = userId,
                FullName = "Old Name",
                Phone = "0123456789",
                DateOfBirth = new DateTime(1990, 1, 1)
            }
        };

        var updateDto = new UpdateSellerInfoDto
        {
            FullName = "New Name",
            PhoneNumber = "0987654321",
            DateOfBirth = new DateTime(1995, 1, 1),
            CompanyName = "New Company",
            TaxId = "123456789",
            CompanyAddress = "New Address"
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Seller, bool>>>(),
            It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _sellerService.UpdateSellerInfoAsync(userId, updateDto);

        // Assert
        result.Should().NotBeNull();
        seller.User.FullName.Should().Be(updateDto.FullName);
        seller.User.Phone.Should().Be(updateDto.PhoneNumber);
        seller.User.DateOfBirth.Should().Be(updateDto.DateOfBirth.Value);
        seller.CompanyName.Should().Be(updateDto.CompanyName);
        seller.TaxId.Should().Be(updateDto.TaxId);
        seller.CompanyAddress.Should().Be(updateDto.CompanyAddress);
        seller.Status.Should().Be(SellerStatus.WaitingReview);
    }

    [Fact]
    public async Task UpdateSellerInfoAsync_ShouldThrowNotFound_WhenSellerNotExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updateDto = new UpdateSellerInfoDto
        {
            FullName = "New Name",
            PhoneNumber = "0987654321",
            DateOfBirth = new DateTime(1995, 1, 1)
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Seller, bool>>>(),
            It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync((Seller)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _sellerService.UpdateSellerInfoAsync(userId, updateDto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(404);
    }

    #endregion

    #region UploadSellerDocumentAsync Tests

    [Fact]
    public async Task UploadSellerDocumentAsync_ShouldUploadDocument_WhenValidFile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = SellerStatus.WaitingReview
        };

        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(1024); // 1KB
        file.Setup(f => f.FileName).Returns("test.pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);

        var fileUrl = "https://storage.com/documents/test.pdf";
        _blobServiceMock.Setup(x => x.GetFileUrlAsync(It.IsAny<string>()))
            .ReturnsAsync(fileUrl);

        // Act
        var result = await _sellerService.UploadSellerDocumentAsync(userId, file.Object);

        // Assert
        result.Should().Be(fileUrl);
        seller.CoaDocumentUrl.Should().Be(fileUrl);
        seller.Status.Should().Be(SellerStatus.WaitingReview);
        _blobServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Once);
    }

    #endregion

    #region GetSellerProfileByIdAsync Tests

    [Fact]
    public async Task GetSellerProfileByIdAsync_ShouldReturnProfile_WhenSellerExists()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = sellerId,
            UserId = Guid.NewGuid(),
            CompanyName = "Test Company",
            User = new User
            {
                RoleName = RoleType.Seller,
                FullName = "Test User",
                Email = "test@example.com"
            }
        };

        _cacheServiceMock.Setup(x => x.GetAsync<Seller>(It.IsAny<string>()))
            .ReturnsAsync((Seller)null);

        _unitOfWorkMock.Setup(x => x.Sellers.GetByIdAsync(sellerId, It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        // Act
        var result = await _sellerService.GetSellerProfileByIdAsync(sellerId);

        // Assert
        result.Should().NotBeNull();
        result.CompanyName.Should().Be(seller.CompanyName);
        result.FullName.Should().Be(seller.User.FullName);
    }

    #endregion

    #region GetSellerProfileByUserIdAsync Tests

    [Fact]
    public async Task GetSellerProfileByUserIdAsync_ShouldReturnProfile_WhenSellerExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyName = "Test Company",
            User = new User
            {
                Id = userId,
                RoleName = RoleType.Seller,
                FullName = "Test User",
                Email = "test@example.com"
            }
        };

        _cacheServiceMock.Setup(x => x.GetAsync<Seller>(It.IsAny<string>()))
            .ReturnsAsync((Seller)null);

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Seller, bool>>>(),
            It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        // Act
        var result = await _sellerService.GetSellerProfileByUserIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.CompanyName.Should().Be(seller.CompanyName);
        result.FullName.Should().Be(seller.User.FullName);
    }

    #endregion

    #region GetAllSellersAsync Tests


    #endregion

    #region GetAllProductsAsync Tests

    [Fact]
    public async Task GetAllProductsAsync_ShouldThrowForbidden_WhenSellerNotVerified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            IsVerified = false
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);

        var param = new ProductQueryParameter { PageIndex = 1, PageSize = 10 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _sellerService.GetAllProductsAsync(param, userId));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(403);
    }

    #endregion

    #region CreateProductAsync Tests

    [Fact]
    public async Task CreateProductAsync_ShouldCreateProduct_WhenSellerIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            IsVerified = true
        };

        var createDto = new ProductSellerCreateDto
        {
            Name = "New Product",
            Description = "Test Description"
        };

        var productCreateDto = new ProductCreateDto
        {
            Name = createDto.Name,
            Description = createDto.Description,
            SellerId = sellerId
        };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);

        _mapperServiceMock.Setup(x => x.Map<ProductSellerCreateDto, ProductCreateDto>(It.IsAny<ProductSellerCreateDto>()))
            .Returns(productCreateDto);

        var expectedResult = new ProducDetailDto
        {
            Id = Guid.NewGuid(),
            Name = createDto.Name,
            Description = createDto.Description
        };

        _productServiceMock.Setup(x => x.CreateAsync(It.Is<ProductCreateDto>(p => 
            p.Name == createDto.Name && 
            p.Description == createDto.Description && 
            p.SellerId == sellerId)))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sellerService.CreateProductAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(createDto.Name);
        result.Description.Should().Be(createDto.Description);
        _productServiceMock.Verify(x => x.CreateAsync(It.Is<ProductCreateDto>(p => 
            p.Name == createDto.Name && 
            p.Description == createDto.Description && 
            p.SellerId == sellerId)), Times.Once);
    }

    [Fact]
    public async Task CreateProductAsync_ShouldThrowForbidden_WhenSellerNotRegistered()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createDto = new ProductSellerCreateDto
        {
            Name = "New Product",
            Description = "Test Description"
        };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync((Seller)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _sellerService.CreateProductAsync(createDto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(403);
        _productServiceMock.Verify(x => x.CreateAsync(It.IsAny<ProductCreateDto>()), Times.Never);
    }

    #endregion

    #region UpdateProductAsync Tests

    [Fact]
    public async Task UpdateProductAsync_ShouldUpdateProduct_WhenSellerOwnsProduct()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            IsVerified = true
        };

        var product = new Product
        {
            Id = productId,
            SellerId = sellerId,
            Name = "Old Name"
        };

        var updateDto = new ProductUpdateDto
        {
            Name = "Updated Name"
        };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);
        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var expectedResult = new ProducDetailDto
        {
            Id = productId,
            Name = updateDto.Name
        };

        _productServiceMock.Setup(x => x.UpdateAsync(productId, updateDto))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sellerService.UpdateProductAsync(productId, updateDto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(updateDto.Name);
    }

    #endregion

    #region DeleteProductAsync Tests

    [Fact]
    public async Task DeleteProductAsync_ShouldDeleteProduct_WhenSellerOwnsProduct()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            IsVerified = true
        };

        var product = new Product
        {
            Id = productId,
            SellerId = sellerId,
            Name = "Product to Delete"
        };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);
        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var expectedResult = new ProducDetailDto
        {
            Id = productId,
            Name = product.Name
        };

        _productServiceMock.Setup(x => x.DeleteAsync(productId))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sellerService.DeleteProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(productId);
    }

    #endregion

    #region UpdateSellerProductImagesAsync Tests

    [Fact]
    public async Task UpdateSellerProductImagesAsync_ShouldUpdateImages_WhenSellerOwnsProduct()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            IsVerified = true
        };

        var product = new Product
        {
            Id = productId,
            SellerId = sellerId
        };

        var images = new List<IFormFile>
        {
            new Mock<IFormFile>().Object
        };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);
        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var expectedResult = new ProducDetailDto
        {
            Id = productId,
            ImageUrls = new List<string> { "image1.jpg" }
        };

        _productServiceMock.Setup(x => x.UpdateProductImagesAsync(productId, images))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sellerService.UpdateSellerProductImagesAsync(productId, images);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(productId);
    }

    #endregion

    #region UpdateSellerAvatarAsync Tests

    [Fact]
    public async Task UpdateSellerAvatarAsync_ShouldUpdateAvatar_WhenValidFile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User
            {
                RoleName = RoleType.Seller,
                Email = "hehe@gmail.com",
                Id = userId,
                AvatarUrl = "old-avatar.jpg"
            }
        };

        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(1024);
        file.Setup(f => f.FileName).Returns("avatar.jpg");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Seller, bool>>>(),
            It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        var newAvatarUrl = "https://storage.com/avatars/new-avatar.jpg";
        _blobServiceMock.Setup(x => x.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync(newAvatarUrl);

        // Act
        var result = await _sellerService.UpdateSellerAvatarAsync(userId, file.Object);

        // Assert
        result.Should().Be(newAvatarUrl);
        seller.User.AvatarUrl.Should().Be(newAvatarUrl);
        _blobServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Once);
    }

    #endregion
}

// Helper classes for async queryable mocking
public class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object Execute(Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression expression)
    {
        return new TestAsyncEnumerable<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        return Execute<TResult>(expression);
    }
}

public class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    {
    }

    public TestAsyncEnumerable(Expression expression)
        : base(expression)
    {
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }
}

public class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        return new ValueTask<bool>(_inner.MoveNext());
    }

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return new ValueTask();
    }
} 