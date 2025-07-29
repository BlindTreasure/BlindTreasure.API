using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MockQueryable.Moq;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class ProductServiceTests
{
    private readonly ProductService _productService;
    private readonly Mock<IBlobService> _blobServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ICategoryService> _categoryServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IMapperService> _mapperServiceMock;
    private readonly Mock<IGenericRepository<Product>> _productRepoMock;
    private readonly Mock<IGenericRepository<Seller>> _sellerRepoMock;
    private readonly Mock<IGenericRepository<Category>> _categoryRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public ProductServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _mapperServiceMock = new Mock<IMapperService>();
        _blobServiceMock = new Mock<IBlobService>();
        _categoryServiceMock = new Mock<ICategoryService>();

        _productRepoMock = new Mock<IGenericRepository<Product>>();
        _sellerRepoMock = new Mock<IGenericRepository<Seller>>();
        _categoryRepoMock = new Mock<IGenericRepository<Category>>();

        _unitOfWorkMock.Setup(x => x.Products).Returns(_productRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Sellers).Returns(_sellerRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepoMock.Object);

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(_currentUserId);

        _productService = new ProductService(
            _unitOfWorkMock.Object,
            _loggerServiceMock.Object,
            _cacheServiceMock.Object,
            _claimsServiceMock.Object,
            _mapperServiceMock.Object,
            _blobServiceMock.Object,
            _categoryServiceMock.Object
        );
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFromCache_WhenCacheExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var cachedProduct = new Product
        {
            Id = productId,
            Name = "Cached Product",
            Description = "Description",
            Price = 100,
            Stock = 10,
            IsDeleted = false,
            Seller = new Seller { CompanyName = "Test Company" }
        };

        _cacheServiceMock.Setup(x => x.GetAsync<Product>(It.IsAny<string>()))
            .ReturnsAsync(cachedProduct);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns(new ProducDetailDto { Id = productId, Name = "Cached Product" });

        // Act
        var result = await _productService.GetByIdAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        result.Name.Should().Be("Cached Product");
        _productRepoMock.Verify(x => x.GetQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFound_WhenProductIsDeleted()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var deletedProduct = new Product
        {
            Id = productId,
            IsDeleted = true
        };

        _cacheServiceMock.Setup(x => x.GetAsync<Product>(It.IsAny<string>()))
            .ReturnsAsync(deletedProduct);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _productService.GetByIdAsync(productId));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(404);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnPaginatedProducts_WhenDataExists()
    {
        // Arrange
        var products = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Product 1",
                Price = 100,
                Stock = 10,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Company 1" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Product 2",
                Price = 200,
                Stock = 20,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Company 2" }
            }
        };

        var mockQueryable = products.AsQueryable().BuildMock();
        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _cacheServiceMock.Setup(x => x.GetAsync<Pagination<ProducDetailDto>>(It.IsAny<string>()))
            .ReturnsAsync((Pagination<ProducDetailDto>)null!);

        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns((Product p) => new ProducDetailDto { Id = p.Id, Name = p.Name });

        var param = new ProductQueryParameter
        {
            PageIndex = 1,
            PageSize = 10
        };

        // Act
        var result = await _productService.GetAllAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        _cacheServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByPriceRangeAndSortByPrice()
    {
        // Arrange
        var products = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Expensive Product",
                Price = 500,
                Stock = 5,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Company 1" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Medium Product",
                Price = 300,
                Stock = 10,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Company 2" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Cheap Product",
                Price = 100,
                Stock = 20,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Company 3" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Out of Range Product",
                Price = 50,
                Stock = 15,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Company 4" }
            }
        };

        var mockQueryable = products.AsQueryable().BuildMock();
        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _cacheServiceMock.Setup(x => x.GetAsync<Pagination<ProducDetailDto>>(It.IsAny<string>()))
            .ReturnsAsync((Pagination<ProducDetailDto>)null!);

        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns((Product p) => new ProducDetailDto 
            { 
                Id = p.Id, 
                Name = p.Name, 
                Price = p.Price,
                Brand = p.Seller?.CompanyName 
            });

        var param = new ProductQueryParameter
        {
            MinPrice = 100,
            MaxPrice = 400,
            SortBy = ProductSortField.Price,
            Desc = false, // ascending order
            PageIndex = 1,
            PageSize = 10
        };

        // Act
        var result = await _productService.GetAllAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2); // Only 2 products in the price range
        var items = result.ToList();
        items.Should().HaveCount(2);
        
        // Check sorting (ascending by price)
        items[0].Name.Should().Be("Cheap Product");
        items[0].Price.Should().Be(100);
        items[1].Name.Should().Be("Medium Product");
        items[1].Price.Should().Be(300);
        
        // The expensive product (500) and cheap product (50) should be filtered out
        items.Should().NotContain(i => i.Name == "Expensive Product" || i.Name == "Out of Range Product");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateProduct_WhenValidData()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var dto = new ProductCreateDto
        {
            Name = "New Product",
            Description = "Description",
            Price = 100,
            Stock = 10,
            CategoryId = categoryId,
            SellerId = sellerId,
            ProductType = ProductSaleType.DirectSale
        };

        var seller = new Seller
        {
            Id = sellerId,
            IsVerified = true,
            Status = SellerStatus.Approved,
            CompanyName = "Test Company"
        };

        var category = new Category
        {
            Id = categoryId,
            Name = "Test Category",
            IsDeleted = false
        };

        _sellerRepoMock.Setup(x => x.GetByIdAsync(sellerId))
            .ReturnsAsync(seller);

        var categories = new List<Category> { category };
        var mockCategoryQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockCategoryQueryable);

        // Setup AddAsync to return a product with proper ID
        _productRepoMock.Setup(x => x.AddAsync(It.IsAny<Product>()))
            .ReturnsAsync((Product p) =>
            {
                p.Id = productId;
                return p;
            });

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Create the product that will be returned by GetByIdAsync
        var createdProduct = new Product
        {
            Id = productId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock,
            SellerId = sellerId,
            Seller = seller,
            IsDeleted = false,
            ProductType = dto.ProductType
        };

        _cacheServiceMock.Setup(x => x.GetAsync<Product>(It.IsAny<string>()))
            .ReturnsAsync((Product)null!);

        // Setup product repository to return the created product
        var products = new List<Product> { createdProduct };
        var mockProductQueryable = products.AsQueryable().BuildMock();
        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockProductQueryable);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns(new ProducDetailDto { Id = productId, Name = dto.Name });

        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _productService.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(dto.Name);
        _productRepoMock.Verify(x => x.AddAsync(It.IsAny<Product>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowForbidden_WhenSellerNotVerified()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var dto = new ProductCreateDto
        {
            Name = "New Product",
            Description = "Description",
            Price = 100,
            Stock = 10,
            CategoryId = Guid.NewGuid(),
            SellerId = sellerId
        };

        var seller = new Seller
        {
            Id = sellerId,
            IsVerified = false,
            Status = SellerStatus.WaitingReview
        };

        _sellerRepoMock.Setup(x => x.GetByIdAsync(sellerId))
            .ReturnsAsync(seller);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _productService.CreateAsync(dto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(403);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateProduct_WhenValidData()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var dto = new ProductUpdateDto
        {
            Name = "Updated Product",
            Description = "Updated Description",
            Price = 150,
            Stock = 20
        };

        var existingProduct = new Product
        {
            Id = productId,
            Name = "Original Product",
            Description = "Original Description",
            Price = 100,
            Stock = 10,
            Status = ProductStatus.Active,
            IsDeleted = false,
            SellerId = Guid.NewGuid(),
            Seller = new Seller
                { Id = Guid.NewGuid(), CompanyName = "Test Seller", IsVerified = true, Status = SellerStatus.Approved }
        };

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(existingProduct);

        _productRepoMock.Setup(x => x.Update(It.IsAny<Product>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetByIdAsync call at the end
        _cacheServiceMock.Setup(x => x.GetAsync<Product>(It.IsAny<string>()))
            .ReturnsAsync((Product)null!);

        var products = new List<Product> { existingProduct };
        var mockQueryable = products.AsQueryable().BuildMock();
        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns(new ProducDetailDto { Id = productId, Name = dto.Name });

        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _productService.UpdateAsync(productId, dto);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        result.Name.Should().Be(dto.Name);
        _productRepoMock.Verify(x => x.Update(It.IsAny<Product>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFound_WhenProductNotExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var dto = new ProductUpdateDto { Name = "Updated" };

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync((Product)null!);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _productService.UpdateAsync(productId, dto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(404);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteProduct_WhenProductExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Product to Delete",
            Status = ProductStatus.Active,
            IsDeleted = false,
            SellerId = Guid.NewGuid(),
            Seller = new Seller { CompanyName = "Test Company" }
        };

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        _productRepoMock.Setup(x => x.Update(It.IsAny<Product>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns(new ProducDetailDto { Id = productId, Name = product.Name });

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _productService.DeleteAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(productId);
        _productRepoMock.Verify(x => x.Update(It.Is<Product>(p => p.IsDeleted && p.Status == ProductStatus.InActive)),
            Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region UploadProductImageAsync Tests

    [Fact]
    public async Task UploadProductImageAsync_ShouldUploadImage_WhenValidFile()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            IsDeleted = false,
            ImageUrls = new List<string>()
        };

        var mockFile = CreateMockFormFile();
        var imageUrl = "https://example.com/image.jpg";

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        _blobServiceMock.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);

        _blobServiceMock.Setup(x => x.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        _productRepoMock.Setup(x => x.Update(It.IsAny<Product>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _productService.UploadProductImageAsync(productId, mockFile);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(imageUrl);
        _blobServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Once);
        _productRepoMock.Verify(x => x.Update(It.Is<Product>(p => p.ImageUrls.Contains(imageUrl))), Times.Once);
    }

    [Fact]
    public async Task UploadProductImageAsync_ShouldThrowBadRequest_WhenFileIsEmpty()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var emptyFile = new FormFile(new MemoryStream(), 0, 0, "Data", "empty.jpg");

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _productService.UploadProductImageAsync(productId, emptyFile));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(400);
    }

    #endregion

    #region UpdateProductImagesAsync Tests

    [Fact]
    public async Task UpdateProductImagesAsync_ShouldUpdateImages_WhenValidFiles()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var oldImageUrl = "https://example.com/old-image.jpg?prefix=products%2Fold-image.jpg";
        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            IsDeleted = false,
            ImageUrls = new List<string> { oldImageUrl },
            Seller = new Seller { CompanyName = "Test Seller" }
        };

        var mockFiles = new List<IFormFile>
        {
            CreateMockFormFile(),
            CreateMockFormFile()
        };

        var newImageUrl = "https://example.com/new-image.jpg";

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        _blobServiceMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _blobServiceMock.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);

        _blobServiceMock.Setup(x => x.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync(newImageUrl);

        _productRepoMock.Setup(x => x.Update(It.IsAny<Product>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Setup for GetByIdAsync call at the end
        var products = new List<Product> { product };
        var mockQueryable = products.AsQueryable().BuildMock();
        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns(new ProducDetailDto { Id = productId, Name = product.Name });

        // Setup cache to return null to force DB query
        _cacheServiceMock.Setup(x => x.GetAsync<Product>(It.IsAny<string>()))
            .ReturnsAsync((Product)null!);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _productService.UpdateProductImagesAsync(productId, mockFiles);

        // Assert
        result.Should().NotBeNull();
        _blobServiceMock.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Once);
        _blobServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Exactly(2));
        
        // Verify that Update was called at least once, but don't verify the exact count of ImageUrls
        _productRepoMock.Verify(x => x.Update(It.IsAny<Product>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateProductImagesAsync_ShouldThrowNotFound_WhenProductNotExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var mockFiles = new List<IFormFile> { CreateMockFormFile() };

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync((Product)null!);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _productService.UpdateProductImagesAsync(productId, mockFiles));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(404);
    }

    #endregion

    #region ApplyProductFiltersAndSorts Tests

    [Fact]
    public async Task ApplyProductFiltersAndSorts_ShouldFilterBySearchAndCategory()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var childCategoryId = Guid.NewGuid();
        var products = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
                CategoryId = childCategoryId,
                Price = 100,
                Stock = 10,
                Status = ProductStatus.Active,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Test Company" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Another Product",
                CategoryId = Guid.NewGuid(),
                Price = 200,
                Stock = 0,
                Status = ProductStatus.Active,
                ProductType = ProductSaleType.DirectSale,
                IsDeleted = false,
                Seller = new Seller { CompanyName = "Another Company" }
            }
        };

        var mockQueryable = products.AsQueryable().BuildMock();
        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        var param = new ProductQueryParameter
        {
            Search = "test",
            CategoryId = categoryId,
            PageIndex = 1,
            PageSize = 10
        };

        _categoryServiceMock.Setup(x => x.GetAllChildCategoryIdsAsync(categoryId))
            .ReturnsAsync(new List<Guid> { childCategoryId });

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns((Product p) => new ProducDetailDto 
            { 
                Id = p.Id, 
                Name = p.Name,
                Brand = p.Seller?.CompanyName
            });

        // Setup cache to return null to force DB query
        _cacheServiceMock.Setup(x => x.GetAsync<Pagination<ProducDetailDto>>(It.IsAny<string>()))
            .ReturnsAsync((Pagination<ProducDetailDto>)null!);

        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _productService.GetAllAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        var items = result.ToList();
        items.Should().HaveCount(1);
        items.First().Name.Should().Be("Test Product");
    }

    #endregion

    #region ValidateProductDto Tests

    [Fact]
    public async Task ValidateProductDto_ShouldThrowBadRequest_WhenInvalidData()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var dto = new ProductCreateDto
        {
            Name = "",
            Description = "Description",
            Price = -100,
            Stock = -1,
            CategoryId = Guid.NewGuid(),
            SellerId = sellerId
        };

        // Setup a valid seller to pass the seller validation
        var seller = new Seller
        {
            Id = sellerId,
            IsVerified = true,
            Status = SellerStatus.Approved,
            CompanyName = "Test Company"
        };

        _sellerRepoMock.Setup(x => x.GetByIdAsync(sellerId))
            .ReturnsAsync(seller);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _productService.CreateAsync(dto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(400);
        ExceptionUtils.ExtractMessage(exception).Should().Contain("Tên sản phẩm không được để trống");
    }

    #endregion

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
}