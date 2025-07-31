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

    /// <summary>
    /// Checks if getting a product by its ID works correctly when the product's information is already saved in the cache.
    /// </summary>
    /// <remarks>
    /// Scenario: Someone asks for a product using its ID, and the system finds that product's details quickly in its temporary storage (cache).
    /// Expected: The product's details are returned right away from the cache, and the system doesn't need to look for it in the main database.
    /// Coverage: How the system handles product requests when the information is readily available in its quick-access memory.
    /// </remarks>
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

    /// <summary>
    /// Checks if trying to get a product that has been marked as deleted results in a 'Not Found' error.
    /// </summary>
    /// <remarks>
    /// Scenario: Someone tries to find a product using its ID, but that product has been removed from active listings (soft-deleted).
    /// Expected: The system responds with a 'Not Found' error (status code 404), indicating the product is no longer available.
    /// Coverage: Ensuring that deleted products cannot be accessed and appropriate error messages are shown.
    /// </remarks>
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

    /// <summary>
    /// Checks if a list of products is returned with pagination details when there are products available.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to get a list of all products, and the system needs to divide them into pages.
    /// Expected: A paginated list of products is returned, showing the total number of products, and this list is also saved in the cache for quicker access later.
    /// Coverage: How the system handles requests for multiple products, including breaking them into manageable pages and speeding up future requests with caching.
    /// </remarks>
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

    /// <summary>
    /// Checks if products are correctly filtered by a price range and sorted by price.
    /// </summary>
    /// <remarks>
    /// Scenario: A user searches for products within a specific price range and wants the results sorted by price.
    /// Expected: Only products within the specified price range are returned, and they are ordered correctly by their price.
    /// Coverage: The system's ability to accurately filter and sort products based on price criteria.
    /// </remarks>
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

    /// <summary>
    /// Checks if a new product can be created successfully when all the provided information is valid.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller provides all necessary and correct details to add a new product to the system.
    /// Expected: The product is successfully added, and its details are returned. The system also makes sure to update its internal records, like caches, to reflect this new product.
    /// Coverage: The process of adding a new product, including ensuring that the seller is authorized and that the product information is saved correctly.
    /// </remarks>
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

    /// <summary>
    /// Checks if a 'Forbidden' error occurs when an unverified seller tries to create a product.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller attempts to add a new product, but their account has not been approved or verified yet.
    /// Expected: The system prevents the product creation with a 'Forbidden' error (status code 403), indicating that only verified sellers can list products.
    /// Coverage: Ensuring that only legitimate and verified sellers can add products to the platform.
    /// </remarks>
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

    /// <summary>
    /// Checks if a product can be successfully updated when valid new information is provided.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller updates the details of an existing product they own with correct information.
    /// Expected: The product's information is updated in the system, and its new details are returned.
    /// Coverage: How products are updated, ensuring data accuracy and reflecting changes in the system's records.
    /// </remarks>
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

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to update a product that doesn't exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to update a product using an ID that does not belong to any existing product.
    /// Expected: The system responds with a 'Not Found' error (status code 404), indicating the product could not be found for an update.
    /// Coverage: Error handling when trying to modify a product that is not in the system.
    /// </remarks>
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

    /// <summary>
    /// Checks if a product is successfully marked as deleted (soft-deleted) when it exists.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to remove a product from active listings, and the product is found in the system.
    /// Expected: The product is marked as deleted and its status is changed to inactive, but it remains in the database for historical purposes.
    /// Coverage: The process of soft-deleting products and updating their status.
    /// </remarks>
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

    /// <summary>
    /// Checks if a product image is successfully uploaded when a valid file is provided.
    /// </summary>
    /// <remarks>
    /// Scenario: A user uploads an image file for a product.
    /// Expected: The image is successfully uploaded to storage, its URL is saved with the product's details, and the image preview URL is returned.
    /// Coverage: Image upload functionality, linking images to products, and ensuring the correct URL is returned.
    /// </remarks>
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

    /// <summary>
    /// Checks if an error occurs when trying to upload an empty image file for a product.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to upload an image file that has no content (is empty).
    /// Expected: The system responds with a 'Bad Request' error (status code 400), indicating that an empty file cannot be uploaded.
    /// Coverage: Input validation for uploaded image files, specifically preventing empty files.
    /// </remarks>
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

    /// <summary>
    /// Checks if a product's images are successfully updated when new valid image files are provided.
    /// </summary>
    /// <remarks>
    /// Scenario: A user updates a product by replacing its existing images with new ones.
    /// Expected: Old images are removed from storage, new images are uploaded, and the product's image URLs are updated accordingly.
    /// Coverage: Updating product images, including handling existing images and uploading new ones.
    /// </remarks>
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

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to update images for a product that doesn't exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to update images for a product using an ID that does not belong to any existing product.
    /// Expected: The system responds with a 'Not Found' error (status code 404), indicating the product could not be found to update its images.
    /// Coverage: Error handling when attempting to modify images for a non-existent product.
    /// </remarks>
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

    /// <summary>
    /// Checks if products are correctly filtered by search terms and category, and then sorted.
    /// </summary>
    /// <remarks>
    /// Scenario: A user searches for products using a keyword and specifies a category, expecting the results to be filtered and sorted appropriately.
    /// Expected: Only products matching the search term within the specified category (including its subcategories) are returned, and they are ordered as requested.
    /// Coverage: The comprehensive filtering and sorting capabilities of the product search, ensuring accurate and relevant results.
    /// </remarks>
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

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when trying to create a product with invalid information.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller attempts to create a product but provides empty or invalid data for required fields (e.g., empty name, negative price or stock).
    /// Expected: The system rejects the request with a 'Bad Request' error (status code 400), indicating that the input data is not acceptable.
    /// Coverage: Input validation for product creation, ensuring that only complete and valid product information is accepted.
    /// </remarks>
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