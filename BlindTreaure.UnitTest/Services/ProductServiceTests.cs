﻿using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class ProductServiceTests
{
    private readonly Mock<IBlobService> _blobServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IGenericRepository<Category>> _categoryServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly Mock<IMapperService> _mapperMock;
    private readonly Mock<IGenericRepository<Product>> _productRepoMock;
    private readonly ProductService _productService;
    private readonly Mock<IGenericRepository<Seller>> _sellerRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    // public ProductServiceTests()
    // {
    //     _cacheServiceMock = new Mock<ICacheService>();
    //     _claimsServiceMock = new Mock<IClaimsService>();
    //     _loggerMock = new Mock<ILoggerService>();
    //     _mapperMock = new Mock<IMapperService>();
    //     _unitOfWorkMock = new Mock<IUnitOfWork>();
    //     _blobServiceMock = new Mock<IBlobService>();
    //     _productRepoMock = new Mock<IGenericRepository<Product>>();
    //     _sellerRepoMock = new Mock<IGenericRepository<Seller>>();
    //     _categoryServiceMock = new Mock<IGenericRepository<Category>>();
    //
    //     _unitOfWorkMock.Setup(x => x.Products).Returns(_productRepoMock.Object);
    //     _unitOfWorkMock.Setup(x => x.Sellers).Returns(_sellerRepoMock.Object);
    //
    //     _productService = new ProductService(
    //         _unitOfWorkMock.Object,
    //         _loggerMock.Object,
    //         _cacheServiceMock.Object,
    //         _claimsServiceMock.Object,
    //         _mapperMock.Object,
    //         _blobServiceMock.Object
    //     );
    // }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProductDto_WhenProductExistsAndNotDeleted()
    {
        // Arrange
        var id = Guid.NewGuid();
        var product = new Product { Id = id, IsDeleted = false };
        var productDto = new ProducDetailDto { Id = id };

        _cacheServiceMock.Setup(x => x.GetAsync<Product>($"product:{id}")).ReturnsAsync((Product)null!);
        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(new List<Product> { product }.AsQueryable());
        _mapperMock.Setup(x => x.Map<Product, ProducDetailDto>(product)).Returns(productDto);

        // Act
        var result = await _productService.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFound_WhenProductIsDeleted()
    {
        // Arrange
        var id = Guid.NewGuid();
        var product = new Product { Id = id, IsDeleted = true };

        _cacheServiceMock.Setup(x => x.GetAsync<Product>($"product:{id}")).ReturnsAsync(product);

        // Act
        Func<Task> act = async () => await _productService.GetByIdAsync(id);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("Không tìm thấy sản phẩm"));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFromCache_WhenCacheHit()
    {
        // Arrange
        var id = Guid.NewGuid();
        var product = new Product { Id = id, IsDeleted = false };
        var productDto = new ProducDetailDto { Id = id };

        _cacheServiceMock.Setup(x => x.GetAsync<Product>($"product:{id}")).ReturnsAsync(product);
        _mapperMock.Setup(x => x.Map<Product, ProducDetailDto>(product)).Returns(productDto);

        // Act
        var result = await _productService.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        _productRepoMock.Verify(x => x.GetQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnPagination_WhenProductsExist()
    {
        // Arrange
        var param = new ProductQueryParameter { PageIndex = 1, PageSize = 10 };
        var products = new List<Product>
        {
            new() { Id = Guid.NewGuid(), IsDeleted = false, Name = "A" },
            new() { Id = Guid.NewGuid(), IsDeleted = false, Name = "B" }
        };
        var productDtos = products.Select(p => new ProducDetailDto { Id = p.Id }).ToList();

        _productRepoMock.Setup(x => x.GetQueryable())
            .Returns(products.AsQueryable());
        _mapperMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns<Product>(p => new ProducDetailDto { Id = p.Id });

        // Act
        var result = await _productService.GetAllAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(products.Count);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateProduct_WhenValid()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seller = new Seller { Id = sellerId, IsVerified = true, Status = SellerStatus.Approved };
        var dto = new ProductCreateDto
        {
            SellerId = sellerId,
            Name = "Test",
            Description = "Desc",
            CategoryId = Guid.NewGuid(),
            Price = 10,
            Stock = 5,
            Status = ProductStatus.Active
        };
        var product = new Product { Id = Guid.NewGuid(), SellerId = sellerId };
        var productDto = new ProducDetailDto { Id = product.Id };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _sellerRepoMock.Setup(x => x.GetByIdAsync(sellerId, It.IsAny<Expression<Func<Seller, object>>[]>()))
            .ReturnsAsync(seller);
        _productRepoMock.Setup(x => x.AddAsync(It.IsAny<Product>())).ReturnsAsync(product);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _mapperMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>())).Returns(productDto);

        // Act
        var result = await _productService.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(product.Id);
        _productRepoMock.Verify(x => x.AddAsync(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowForbidden_WhenSellerNotVerified()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seller = new Seller { Id = sellerId, IsVerified = false, Status = SellerStatus.WaitingReview };
        var dto = new ProductCreateDto { SellerId = sellerId };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _sellerRepoMock.Setup(x => x.GetByIdAsync(sellerId, It.IsAny<Expression<Func<Seller, object>>[]>()))
            .ReturnsAsync(seller);

        // Act
        Func<Task> act = async () => await _productService.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("Seller chưa được xác minh"));
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateProduct_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var product = new Product { Id = id, IsDeleted = false, SellerId = Guid.NewGuid() };
        var dto = new ProductUpdateDto { Name = "Updated", Description = "Desc" };
        var productDto = new ProducDetailDto { Id = id };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _productRepoMock.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(product);
        _unitOfWorkMock.Setup(x => x.Products.Update(product)).ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _mapperMock.Setup(x => x.Map<Product, ProducDetailDto>(product)).Returns(productDto);

        // Act
        var result = await _productService.UpdateAsync(id, dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(id);
        _unitOfWorkMock.Verify(x => x.Products.Update(product), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFound_WhenProductNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dto = new ProductUpdateDto();

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _productRepoMock.Setup(x => x.GetByIdAsync(id)).ReturnsAsync((Product)null!);

        // Act
        Func<Task> act = async () => await _productService.UpdateAsync(id, dto);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("Không tìm thấy sản phẩm"));
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteProduct_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var product = new Product { Id = id, IsDeleted = false, SellerId = Guid.NewGuid() };
        var productDto = new ProducDetailDto { Id = id };

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _productRepoMock.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(product);
        _unitOfWorkMock.Setup(x => x.Products.Update(product)).ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _mapperMock.Setup(x => x.Map<Product, ProducDetailDto>(product)).Returns(productDto);

        // Act
        var result = await _productService.DeleteAsync(id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(id);
        product.IsDeleted.Should().BeTrue();
        _unitOfWorkMock.Verify(x => x.Products.Update(product), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowNotFound_WhenProductNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);
        _productRepoMock.Setup(x => x.GetByIdAsync(id)).ReturnsAsync((Product)null!);

        // Act
        Func<Task> act = async () => await _productService.DeleteAsync(id);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("Không tìm thấy sản phẩm"));
    }
}