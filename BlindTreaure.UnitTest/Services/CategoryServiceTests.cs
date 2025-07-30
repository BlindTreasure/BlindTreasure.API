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

    /// <summary>
    /// Tests if a `NotFound` exception is thrown when `GetByIdAsync` is called for a non-existent category.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to retrieve a category by an ID that does not exist in the database or cache.
    /// Expected: An `Exception` with a 404 (Not Found) status code is thrown.
    /// Coverage: Error handling for retrieving non-existent categories.
    /// </remarks>
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

    /// <summary>
    /// Tests if `GetAllAsync` returns a paginated list of categories when data exists.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to retrieve all categories with pagination parameters.
    /// Expected: A `PaginationResponse<Category>` containing the categories is returned, and the result is cached.
    /// Coverage: Pagination functionality and caching of category lists.
    /// </remarks>
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

    #region GetAllChildCategoryIdsAsync Tests

    /// <summary>
    /// Checks if the system correctly finds all subcategory IDs, including grand-children and beyond, for a given parent category.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to get all IDs of categories that fall under a specific parent category, regardless of how many levels deep they are.
    /// Expected: A list containing the ID of the parent category and all its subcategory IDs is returned.
    /// Coverage: The ability to trace all related subcategories within the system's hierarchy.
    /// </remarks>
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

    #region GetCategoriesWithAllProductsAsync Tests

    /// <summary>
    /// Checks if categories are returned along with all products, including those in subcategories.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to retrieve categories and all their associated products, covering products directly in the category and those in its subcategories.
    /// Expected: A list of categories, with each category showing the total count of all products under it.
    /// Coverage: Retrieving category data with a comprehensive count of all related products.
    /// </remarks>
    [Fact]
    public async Task GetCategoriesWithAllProductsAsync_ShouldReturnCategoriesWithProducts()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var categories = new List<Category>
        {
            new()
            {
                Id = parentId,
                Name = "Parent Category",
                ParentId = null,
                IsDeleted = false,
                Products = new List<Product>
                {
                    new() { Id = Guid.NewGuid(), Name = "Product 1", IsDeleted = false }
                },
                Children = new List<Category>
                {
                    new()
                    {
                        Id = childId,
                        Name = "Child Category",
                        ParentId = parentId,
                        IsDeleted = false,
                        Products = new List<Product>
                        {
                            new() { Id = Guid.NewGuid(), Name = "Child Product", IsDeleted = false }
                        }
                    }
                }
            }
        };

        var mockQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        _mapperServiceMock.Setup(x => x.Map<Product, ProducDetailDto>(It.IsAny<Product>()))
            .Returns((Product p) => new ProducDetailDto { Id = p.Id, Name = p.Name });

        // Act
        var result = await _categoryService.GetCategoriesWithAllProductsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].ProductCount.Should().Be(2); // 1 parent product + 1 child product
    }

    /// <summary>
    /// Checks if an empty list is returned when there are no categories to retrieve.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to get all categories, but no categories exist in the system.
    /// Expected: An empty list is returned, indicating no categories were found.
    /// Coverage: Handling cases where no categories are present in the database.
    /// </remarks>
    [Fact]
    public async Task GetCategoriesWithAllProductsAsync_ShouldReturnEmptyList_WhenNoCategories()
    {
        // Arrange
        var mockQueryable = new List<Category>().AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act
        var result = await _categoryService.GetCategoriesWithAllProductsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetWithParentAsync Tests

    /// <summary>
    /// Checks if a category is returned along with its direct parent category.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to retrieve a specific category, and it has a parent category.
    /// Expected: The category is returned, and its `Parent` property is correctly populated with the parent category's details.
    /// Coverage: Retrieving category information including its immediate parent in the hierarchy.
    /// </remarks>
    [Fact]
    public async Task GetWithParentAsync_ShouldReturnCategoryWithParent()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var categories = new List<Category>
        {
            new()
            {
                Id = parentId,
                Name = "Parent Category",
                ParentId = null,
                IsDeleted = false
            },
            new()
            {
                Id = childId,
                Name = "Child Category",
                ParentId = parentId,
                IsDeleted = false,
                Parent = new Category
                {
                    Id = parentId,
                    Name = "Parent Category",
                    ParentId = null,
                    IsDeleted = false
                }
            }
        };

        var mockQueryable = categories.AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act
        var result = await _categoryService.GetWithParentAsync(childId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(childId);
        result.Parent.Should().NotBeNull();
        result.Parent!.Id.Should().Be(parentId);
    }

    /// <summary>
    /// Checks if `null` is returned when trying to get a category that doesn't exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to find a category by an ID that isn't in the system.
    /// Expected: Nothing is returned (a `null` value), meaning no category was found.
    /// Coverage: How the system handles requests for categories that don't exist.
    /// </remarks>
    [Fact]
    public async Task GetWithParentAsync_ShouldReturnNull_WhenCategoryNotExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var mockQueryable = new List<Category>().AsQueryable().BuildMock();
        _categoryRepoMock.Setup(x => x.GetQueryable())
            .Returns(mockQueryable);

        // Act
        var result = await _categoryService.GetWithParentAsync(categoryId);

        // Assert
        result.Should().BeNull();
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
    /// Checks if a category is correctly returned when it exists in the system.
    /// </summary>
    /// <remarks>
    /// Scenario: A request is made to find a category by its ID, and that category exists.
    /// Expected: The category's details are returned.
    /// Coverage: Successfully retrieving category information when the category is present.
    /// </remarks>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnCategoryDto_WhenCategoryExists()
    {
        var categoryId = Guid.NewGuid();
        var category = new Category
            { Id = categoryId, Name = "Test", Description = "Desc", Children = new List<Category>() };
        var mockSet = new List<Category> { category }.AsQueryable().BuildMockDbSet();
        _unitOfWorkMock.Setup(x => x.Categories.GetQueryable()).Returns(mockSet.Object);
        _cacheServiceMock.Setup(x => x.GetAsync<Category>($"category:{categoryId}")).ReturnsAsync((Category)null!);
        var result = await _categoryService.GetByIdAsync(categoryId);
        result.Should().NotBeNull();
        result.Id.Should().Be(categoryId);
    }

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