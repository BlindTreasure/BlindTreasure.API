using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class CategoryServiceTests
{
    private readonly CategoryService _categoryService;
    private readonly Mock<IBlobService> _blobServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IGenericRepository<Category>> _categoryRepoMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IMapperService> _mapperServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserService> _userServiceMock;
//trigger
    public CategoryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _userServiceMock = new Mock<IUserService>();
        _blobServiceMock = new Mock<IBlobService>();
        _mapperServiceMock = new Mock<IMapperService>();

        _categoryRepoMock = new Mock<IGenericRepository<Category>>();
        _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepoMock.Object);

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(_currentUserId);

        _categoryService = new CategoryService(
            _unitOfWorkMock.Object,
            _loggerServiceMock.Object,
            _cacheServiceMock.Object,
            _claimsServiceMock.Object,
            _userServiceMock.Object,
            _blobServiceMock.Object,
            _mapperServiceMock.Object
        );
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFromCache_WhenCacheExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var cachedCategory = new Category
        {
            Id = categoryId,
            Name = "Cached Category",
            Description = "Description",
            IsDeleted = false
        };

        _cacheServiceMock.Setup(x => x.GetAsync<Category>(It.IsAny<string>()))
            .ReturnsAsync(cachedCategory);

        // Act
        var result = await _categoryService.GetByIdAsync(categoryId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(categoryId);
        result.Name.Should().Be("Cached Category");
        _categoryRepoMock.Verify(x => x.GetQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFound_WhenCategoryNotExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        _cacheServiceMock.Setup(x => x.GetAsync<Category>(It.IsAny<string>()))
            .ReturnsAsync((Category)null!);

        var mockQueryable = new List<Category>().AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _categoryService.GetByIdAsync(categoryId));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(404);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnPaginatedCategories_WhenDataExists()
    {
        // Arrange
        var categories = new List<Category>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Category 1",
                ParentId = null,
                IsDeleted = false,
                Children = new List<Category>()
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Category 2",
                ParentId = null,
                IsDeleted = false,
                Children = new List<Category>()
            }
        };

        var mockQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        var param = new CategoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10
        };

        // Act
        var result = await _categoryService.GetAllAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        _cacheServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()),
            Times.Once);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateCategory_WhenValidData()
    {
        // Arrange
        var dto = new CategoryCreateDto
        {
            Name = "New Category",
            Description = "Description"
        };

        var user = new UserDto
        {
            FullName = "Admin User",
            RoleName = RoleType.Admin
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        var emptyQueryable = new List<Category>().AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(emptyQueryable);

        _categoryRepoMock.Setup(x => x.AddAsync(It.IsAny<Category>()))
            .ReturnsAsync((Category c) => c);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _categoryService.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(dto.Name);
        _categoryRepoMock.Verify(x => x.AddAsync(It.IsAny<Category>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowConflict_WhenCategoryNameExists()
    {
        // Arrange
        var dto = new CategoryCreateDto
        {
            Name = "Existing Category"
        };

        var user = new UserDto
        {
            FullName = "Admin User",
            RoleName = RoleType.Admin
        };

        var existingCategories = new List<Category>
        {
            new() { Id = Guid.NewGuid(), Name = "Existing Category", IsDeleted = false }
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        var mockQueryable = existingCategories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _categoryService.CreateAsync(dto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(409);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateCategory_WhenValidData()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var dto = new CategoryUpdateDto
        {
            Name = "Updated Category",
            Description = "Updated Description"
        };

        var user = new UserDto
        {
            FullName = "Admin User",
            RoleName = RoleType.Admin
        };

        var existingCategory = new Category
        {
            Id = categoryId,
            Name = "Original Category",
            Description = "Original Description",
            IsDeleted = false,
            Children = new List<Category>()
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        var categories = new List<Category> { existingCategory };
        var mockQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _unitOfWorkMock.Setup(x => x.Categories.Update(It.IsAny<Category>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _categoryService.UpdateAsync(categoryId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(dto.Name);
        _unitOfWorkMock.Verify(x => x.Categories.Update(It.IsAny<Category>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowForbidden_WhenUserNotAuthorized()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var dto = new CategoryUpdateDto { Name = "Updated" };

        var user = new UserDto
        {
            FullName = "Customer User",
            RoleName = RoleType.Customer
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _categoryService.UpdateAsync(categoryId, dto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(403);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteCategory_WhenValidAndNoChildren()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var user = new UserDto
        {
            FullName = "Admin User",
            RoleName = RoleType.Admin
        };

        var category = new Category
        {
            Id = categoryId,
            Name = "Category to Delete",
            IsDeleted = false,
            Products = new List<Product>(),
            Children = new List<Category>()
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        var categories = new List<Category> { category };
        var mockQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _unitOfWorkMock.Setup(x => x.Categories.SoftRemove(It.IsAny<Category>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _categoryService.DeleteAsync(categoryId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(categoryId);
        _unitOfWorkMock.Verify(x => x.Categories.SoftRemove(It.IsAny<Category>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowConflict_WhenCategoryHasProducts()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var user = new UserDto
        {
            FullName = "Admin User",
            RoleName = RoleType.Admin
        };

        var category = new Category
        {
            Id = categoryId,
            Name = "Category with Products",
            IsDeleted = false,
            Products = new List<Product> { new() { Id = Guid.NewGuid() } },
            Children = new List<Category>()
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        var categories = new List<Category> { category };
        var mockQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _categoryService.DeleteAsync(categoryId));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(409);
    }

    #endregion

    #region GetAllChildCategoryIdsAsync Tests

    [Fact]
    public async Task GetAllChildCategoryIdsAsync_ShouldReturnAllDescendantIds()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId1 = Guid.NewGuid();
        var childId2 = Guid.NewGuid();
        var grandChildId = Guid.NewGuid();

        var categories = new List<Category>
        {
            new() { Id = parentId, ParentId = null, IsDeleted = false },
            new() { Id = childId1, ParentId = parentId, IsDeleted = false },
            new() { Id = childId2, ParentId = parentId, IsDeleted = false },
            new() { Id = grandChildId, ParentId = childId1, IsDeleted = false }
        };

        var mockQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act
        var result = await _categoryService.GetAllChildCategoryIdsAsync(parentId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4); // parent + 2 children + 1 grandchild
        result.Should().Contain(new[] { parentId, childId1, childId2, grandChildId });
    }

    #endregion
}