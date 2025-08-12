using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Resend;

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

    #region CreateAsync Tests

    /// <summary>
    /// Tests if a new category can be created successfully with valid input data.
    /// </summary>
    /// <remarks>
    /// Scenario: An admin user provides valid information to create a new category.
    /// Expected: A `CategoryDto` representing the newly created category is returned, the category is added to the database, and relevant caches are invalidated.
    /// Coverage: Category creation, role-based access control, and cache management.
    /// </remarks>
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

    /// <summary>
    /// Tests if a `Conflict` exception is thrown when attempting to create a category with an existing name.
    /// </summary>
    /// <remarks>
    /// Scenario: An admin user attempts to create a category with a name that is already in use by another category.
    /// Expected: An `Exception` with a 409 (Conflict) status code is thrown.
    /// Coverage: Category name uniqueness validation.
    /// </remarks>
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

    /// <summary>
    /// Tests if a `Forbidden` exception is thrown when a non-admin user attempts to create a category.
    /// </summary>
    /// <remarks>
    /// Scenario: A user without administrator privileges attempts to create a new category.
    /// Expected: An `Exception` with a 403 (Forbidden) status code is thrown.
    /// Coverage: Role-based access control for category creation.
    /// </remarks>
    [Fact]
    public async Task CreateAsync_ShouldThrowForbidden_WhenUserNotAdmin()
    {
        // Arrange
        var dto = new CategoryCreateDto { Name = "New Category" };
        var user = new UserDto
        {
            FullName = "Regular User",
            RoleName = RoleType.Customer
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => _categoryService.CreateAsync(dto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(403);
    }

    #endregion

    #region UpdateAsync Tests

    /// <summary>
    /// Checks if a category can be updated successfully when given valid information.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator changes the details of an existing category with correct information.
    /// Expected: The category is updated in the system, and its new details are returned.
    /// Coverage: How categories are updated, ensuring only authorized users can do it, and making sure the system's saved information is fresh.
    /// </remarks>
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

    /// <summary>
    /// Checks if an error occurs when a user without permission tries to update a category.
    /// </summary>
    /// <remarks>
    /// Scenario: A user who is not an administrator tries to change a category.
    /// Expected: The system stops the action with a 'Forbidden' error (status code 403), showing that the user doesn't have the right permissions.
    /// Coverage: Making sure only authorized users can update categories.
    /// </remarks>
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

    /// <summary>
    /// Checks if a category can be successfully deleted when it has no subcategories or products.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator deletes a category that doesn't have any products or smaller categories linked to it.
    /// Expected: The category is marked as deleted in the system, and its details are returned.
    /// Coverage: How categories are deleted, making sure only authorized users can do it, and ensuring categories are empty before deletion.
    /// </remarks>
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

    /// <summary>
    /// Checks if an error occurs when trying to delete a category that still has products linked to it.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator tries to delete a category that has products associated with it.
    /// Expected: The system stops the action with a 'Conflict' error (status code 409), indicating that the category cannot be deleted while products are linked.
    /// Coverage: Preventing deletion of categories that are still in use.
    /// </remarks>
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

    #region UpdateAsync Additional Tests

    /// <summary>
    /// Checks if an error occurs when trying to update a category that doesn't exist.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator attempts to update a category using an ID that does not match any existing category.
    /// Expected: The system stops the action with a 'Not Found' error (status code 404), indicating the category doesn't exist.
    /// Coverage: Error handling when trying to update a non-existent category.
    /// </remarks>
    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFound_WhenCategoryNotExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var dto = new CategoryUpdateDto { Name = "Updated Name" };

        var user = new UserDto
        {
            FullName = "Admin User",
            RoleName = RoleType.Admin
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        var mockQueryable = new List<Category>().AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _categoryService.UpdateAsync(categoryId, dto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(404);
    }

    #endregion

    #region DeleteAsync Additional Tests

    /// <summary>
    /// Checks if an error occurs when a user without administrative or staff privileges tries to delete a category.
    /// </summary>
    /// <remarks>
    /// Scenario: A regular user (neither admin nor staff) attempts to delete a category.
    /// Expected: The system stops the action with a 'Forbidden' error (status code 403), showing that the user doesn't have the right permissions.
    /// Coverage: Ensuring only authorized personnel can delete categories.
    /// </remarks>
    [Fact]
    public async Task DeleteAsync_ShouldThrowForbidden_WhenUserNotAdminOrStaff()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var user = new UserDto
        {
            FullName = "Customer User",
            RoleName = RoleType.Customer
        };

        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(_currentUserId))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _categoryService.DeleteAsync(categoryId));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(403);
    }

    /// <summary>
    /// Checks if an error occurs when trying to delete a category that still has subcategories.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator tries to delete a category that has other categories listed under it.
    /// Expected: The system stops the action with a 'Conflict' error (status code 409), indicating that the category cannot be deleted while it has subcategories.
    /// Coverage: Preventing deletion of categories that are still organizing other categories.
    /// </remarks>
    [Fact]
    public async Task DeleteAsync_ShouldThrowConflict_WhenCategoryHasChildren()
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
            Name = "Category with Children",
            IsDeleted = false,
            Products = new List<Product>(),
            Children = new List<Category>
            {
                new() { Id = Guid.NewGuid(), IsDeleted = false }
            }
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


    /// <summary>
    /// Checks if an error occurs when trying to create a category with an empty name.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator attempts to create a new category but leaves the name field blank.
    /// Expected: The system prevents the creation with an error, as a category name cannot be empty.
    /// Coverage: Input validation for category names.
    /// </remarks>
    [Fact]
    public async Task CreateAsync_ShouldThrowBadRequest_WhenNameIsEmpty()
    {
        var dto = new CategoryCreateDto { Name = "" };
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(Guid.NewGuid());
        _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new UserDto { RoleName = RoleType.Admin });
        await Assert.ThrowsAsync<Exception>(() => _categoryService.CreateAsync(dto));
    }
}