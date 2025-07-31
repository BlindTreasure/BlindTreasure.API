using BlindTreasure.Domain;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using BlindTreasure.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BlindTreaure.UnitTest.Infrastructure;

public class GenericRepositoryTests
{
    private readonly BlindTreasureDbContext _dbContext;
    private readonly Mock<ICurrentTime> _timeServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly GenericRepository<User> _genericRepository;

    public GenericRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BlindTreasureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // Use unique name for each test run
            .Options;

        _dbContext = new BlindTreasureDbContext(options);
        _timeServiceMock = new Mock<ICurrentTime>();
        _claimsServiceMock = new Mock<IClaimsService>();

        _genericRepository = new GenericRepository<User>(
            _dbContext,
            _timeServiceMock.Object,
            _claimsServiceMock.Object
        );
    }

    #region AddAsync Tests

    /// <summary>
    /// Tests that AddAsync properly sets timestamps and user tracking properties
    /// </summary>
    /// <remarks>
    /// Scenario: Adding a new entity to the database
    /// Expected: Entity is added with correct timestamps and creator ID
    /// Coverage: AddAsync method implementation
    /// </remarks>
    [Fact]
    public async Task AddAsync_ShouldAddEntityWithTimestamps()
    {
        // Arrange
        var currentTime = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        _timeServiceMock.Setup(x => x.GetCurrentTime()).Returns(currentTime);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);

        var user = new User
            { Id = Guid.NewGuid(), FullName = "Test User", RoleName = RoleType.Customer, Email = "hehe@gmail.com" };

        // Act
        var result = await _genericRepository.AddAsync(user);
        await _dbContext.SaveChangesAsync(); // Save changes to in-memory database

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().Be(currentTime.ToUniversalTime());
        result.UpdatedAt.Should().Be(currentTime.ToUniversalTime());
        result.CreatedBy.Should().Be(userId);

        // Verify entity was added to the database
        var savedUser = await _dbContext.Set<User>().FindAsync(user.Id);
        savedUser.Should().NotBeNull();
        savedUser.Id.Should().Be(user.Id);
    }

    #endregion

    #region GetByIdAsync Tests

    /// <summary>
    /// Tests retrieving an entity by ID when the entity exists
    /// </summary>
    /// <remarks>
    /// Scenario: Retrieving an existing entity by its ID
    /// Expected: The correct entity is returned with all properties
    /// Coverage: GetByIdAsync method implementation
    /// </remarks>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntity_WhenEntityExists()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test User",
            RoleName = RoleType.Customer,
            Email = "test@example.com"
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _genericRepository.GetByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.FullName.Should().Be("Test User");
    }

    /// <summary>
    /// Tests retrieving an entity by ID when the entity doesn't exist
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to retrieve a non-existent entity by ID
    /// Expected: Null is returned
    /// Coverage: GetByIdAsync method null handling
    /// </remarks>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenEntityNotExists()
    {
        // Act
        var result = await _genericRepository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region SoftRemove Tests

    /// <summary>
    /// Tests the soft delete functionality for an entity
    /// </summary>
    /// <remarks>
    /// Scenario: Soft deleting an existing entity
    /// Expected: Entity is marked as deleted with timestamp and user tracking info
    /// Coverage: SoftRemove method implementation
    /// </remarks>
    [Fact]
    public async Task SoftRemove_ShouldMarkEntityAsDeleted()
    {
        // Arrange
        var currentTime = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test User",
            RoleName = RoleType.Customer,
            Email = "delete@example.com"
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        _timeServiceMock.Setup(x => x.GetCurrentTime()).Returns(currentTime);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);

        // Act
        var result = await _genericRepository.SoftRemove(user);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().Be(currentTime.ToUniversalTime());
        user.DeletedBy.Should().Be(userId);

        var updatedUser = await _dbContext.Users.FindAsync(user.Id);
        updatedUser.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Update Tests

    /// <summary>
    /// Tests updating an entity with proper timestamp and user tracking
    /// </summary>
    /// <remarks>
    /// Scenario: Updating an existing entity's properties
    /// Expected: Entity is updated with correct timestamp and user tracking info
    /// Coverage: Update method implementation
    /// </remarks>
    [Fact]
    public async Task Update_ShouldUpdateEntityWithTimestamp()
    {
        // Arrange
        var currentTime = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Old Name",
            RoleName = RoleType.Customer,
            Email = "update@example.com"
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        _timeServiceMock.Setup(x => x.GetCurrentTime()).Returns(currentTime);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);

        user.FullName = "New Name";

        // Act
        var result = await _genericRepository.Update(user);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeTrue();
        user.UpdatedAt.Should().Be(currentTime);
        user.UpdatedBy.Should().Be(userId);
        user.FullName.Should().Be("New Name");

        var updatedUser = await _dbContext.Users.FindAsync(user.Id);
        updatedUser.FullName.Should().Be("New Name");
    }

    #endregion

    #region FirstOrDefaultAsync Tests

    /// <summary>
    /// Tests finding the first entity that matches a predicate
    /// </summary>
    /// <remarks>
    /// Scenario: Finding an entity using a predicate that matches at least one item
    /// Expected: The first matching entity is returned
    /// Coverage: FirstOrDefaultAsync method implementation
    /// </remarks>
    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnEntity_WhenPredicateMatches()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test User",
            RoleName = RoleType.Customer,
            Email = "first@example.com"
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _genericRepository.FirstOrDefaultAsync(u => u.Email == "first@example.com");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("first@example.com");
    }

    #endregion

    #region AddRangeAsync Tests

    /// <summary>
    /// Tests adding multiple entities in a single operation
    /// </summary>
    /// <remarks>
    /// Scenario: Adding a collection of entities to the database
    /// Expected: All entities are added successfully
    /// Coverage: AddRangeAsync method implementation
    /// </remarks>
    [Fact]
    public async Task AddRangeAsync_ShouldAddMultipleEntities()
    {
        // Arrange
        var currentTime = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        _timeServiceMock.Setup(x => x.GetCurrentTime()).Returns(currentTime);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);

        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "User 1",
                RoleName = RoleType.Customer,
                Email = "user1@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "User 2",
                RoleName = RoleType.Customer,
                Email = "user2@example.com"
            }
        };

        // Act
        await _genericRepository.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedUsers = await _dbContext.Users.ToListAsync();
        savedUsers.Should().HaveCount(2);
        savedUsers.Should().Contain(u => u.Email == "user1@example.com");
        savedUsers.Should().Contain(u => u.Email == "user2@example.com");
    }

    #endregion

    #region GetAllAsync Tests

    /// <summary>
    /// Tests retrieving all entities when no filter is applied
    /// </summary>
    /// <remarks>
    /// Scenario: Fetching all entities without any filtering
    /// Expected: All entities are returned
    /// Coverage: GetAllAsync method with null predicate
    /// </remarks>
    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities_WhenPredicateIsNull()
    {
        // Arrange
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "User A",
                RoleName = RoleType.Customer,
                Email = "userA@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "User B",
                RoleName = RoleType.Customer,
                Email = "userB@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _genericRepository.GetAllAsync(null);

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().Contain(u => u.Email == "userA@example.com");
        result.Should().Contain(u => u.Email == "userB@example.com");
    }

    /// <summary>
    /// Tests retrieving entities with a filtering predicate
    /// </summary>
    /// <remarks>
    /// Scenario: Fetching entities using a filter condition
    /// Expected: Only entities matching the predicate are returned
    /// Coverage: GetAllAsync method with predicate filtering
    /// </remarks>
    [Fact]
    public async Task GetAllAsync_ShouldReturnFilteredEntities_WhenPredicateIsProvided()
    {
        // Arrange
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Admin User",
                RoleName = RoleType.Admin,
                Email = "admin@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Customer User",
                RoleName = RoleType.Customer,
                Email = "customer@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _genericRepository.GetAllAsync(u => u.RoleName == RoleType.Admin);

        // Assert
        result.Should().HaveCount(1);
        result.First().Email.Should().Be("admin@example.com");
    }

    #endregion

    #region SoftRemoveRange Tests

    /// <summary>
    /// Tests soft deleting multiple entities in a single operation
    /// </summary>
    /// <remarks>
    /// Scenario: Soft deleting a collection of entities
    /// Expected: All entities are marked as deleted with proper tracking info
    /// Coverage: SoftRemoveRange method implementation
    /// </remarks>
    [Fact]
    public async Task SoftRemoveRange_ShouldMarkMultipleEntitiesAsDeleted()
    {
        // Arrange
        var currentTime = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Delete User 1",
                RoleName = RoleType.Customer,
                Email = "delete1@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Delete User 2",
                RoleName = RoleType.Customer,
                Email = "delete2@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        _timeServiceMock.Setup(x => x.GetCurrentTime()).Returns(currentTime);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);

        // Act
        var result = await _genericRepository.SoftRemoveRange(users);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeTrue();
        users.All(u => u.IsDeleted).Should().BeTrue();
        users.All(u => u.DeletedBy == userId).Should().BeTrue();

        var deletedUsers = await _dbContext.Users
            .Where(u => u.Email == "delete1@example.com" || u.Email == "delete2@example.com")
            .ToListAsync();
        deletedUsers.All(u => u.IsDeleted).Should().BeTrue();
    }

    #endregion

    #region SoftRemoveRangeById Tests

    /// <summary>
    /// Tests soft deleting entities by providing a list of IDs
    /// </summary>
    /// <remarks>
    /// Scenario: Soft deleting multiple entities by their IDs
    /// Expected: All entities with matching IDs are marked as deleted
    /// Coverage: SoftRemoveRangeById method implementation
    /// </remarks>
    [Fact]
    public async Task SoftRemoveRangeById_ShouldMarkEntitiesAsDeleted_WhenIdsExist()
    {
        // Arrange
        var currentTime = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Delete By Id 1",
                RoleName = RoleType.Customer,
                Email = "deletebyid1@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Delete By Id 2",
                RoleName = RoleType.Customer,
                Email = "deletebyid2@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        _timeServiceMock.Setup(x => x.GetCurrentTime()).Returns(currentTime);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);

        var userIds = users.Select(u => u.Id).ToList();

        // Act
        var result = await _genericRepository.SoftRemoveRangeById(userIds);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeTrue();

        var deletedUsers = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();
        deletedUsers.All(u => u.IsDeleted).Should().BeTrue();
        deletedUsers.All(u => u.DeletedBy == userId).Should().BeTrue();
    }

    #endregion

    #region UpdateRange Tests

    /// <summary>
    /// Tests updating multiple entities in a single operation
    /// </summary>
    /// <remarks>
    /// Scenario: Updating a collection of entities
    /// Expected: All entities are updated with proper tracking information
    /// Coverage: UpdateRange method implementation
    /// </remarks>
    [Fact]
    public async Task UpdateRange_ShouldUpdateMultipleEntities()
    {
        // Arrange
        var currentTime = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Update User 1",
                RoleName = RoleType.Customer,
                Email = "update1@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Update User 2",
                RoleName = RoleType.Customer,
                Email = "update2@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        _timeServiceMock.Setup(x => x.GetCurrentTime()).Returns(currentTime);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(userId);

        // Modify users
        users[0].FullName = "Updated User 1";
        users[1].FullName = "Updated User 2";

        // Act
        var result = await _genericRepository.UpdateRange(users);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeTrue();
        users.All(u => u.UpdatedAt == currentTime).Should().BeTrue();
        users.All(u => u.UpdatedBy == userId).Should().BeTrue();

        var updatedUsers = await _dbContext.Users
            .Where(u => u.Email == "update1@example.com" || u.Email == "update2@example.com")
            .ToListAsync();
        updatedUsers.Should().Contain(u => u.FullName == "Updated User 1");
        updatedUsers.Should().Contain(u => u.FullName == "Updated User 2");
    }

    #endregion

    #region GetQueryable Tests

    /// <summary>
    /// Tests retrieving a queryable collection of entities
    /// </summary>
    /// <remarks>
    /// Scenario: Getting an IQueryable object for custom querying
    /// Expected: A valid IQueryable instance is returned
    /// Coverage: GetQueryable method implementation
    /// </remarks>
    [Fact]
    public void GetQueryable_ShouldReturnQueryable()
    {
        // Act
        var result = _genericRepository.GetQueryable();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IQueryable<User>>();
    }

    #endregion

    #region CountAsync Tests

    /// <summary>
    /// Tests counting entities that match a given criteria
    /// </summary>
    /// <remarks>
    /// Scenario: Counting entities with specific filtering conditions
    /// Expected: The correct count of matching entities is returned
    /// Coverage: CountAsync method implementation
    /// </remarks>
    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Count User 1",
                RoleName = RoleType.Customer,
                Email = "count1@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Count User 2",
                RoleName = RoleType.Customer,
                Email = "count2@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Count User 3",
                RoleName = RoleType.Admin,
                Email = "count3@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        // Act
        var customerCount = await _genericRepository.CountAsync(u => u.RoleName == RoleType.Customer);
        var adminCount = await _genericRepository.CountAsync(u => u.RoleName == RoleType.Admin);

        // Assert
        customerCount.Should().Be(2);
        adminCount.Should().Be(1);
    }

    #endregion

    #region HardRemove Tests

    /// <summary>
    /// Tests permanently removing entities that match a predicate
    /// </summary>
    /// <remarks>
    /// Scenario: Permanently deleting entities matching specific criteria
    /// Expected: All matching entities are completely removed from the database
    /// Coverage: HardRemove method implementation
    /// </remarks>
    [Fact]
    public async Task HardRemove_ShouldPermanentlyRemoveEntities()
    {
        // Arrange
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Hard Delete 1",
                RoleName = RoleType.Customer,
                Email = "harddelete@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Hard Delete 2",
                RoleName = RoleType.Customer,
                Email = "harddelete@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _genericRepository.HardRemove(u => u.Email == "harddelete@example.com");
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeTrue();
        var remainingUsers = await _dbContext.Users.Where(u => u.Email == "harddelete@example.com").ToListAsync();
        remainingUsers.Should().BeEmpty();
    }

    #endregion

    #region HardRemoveRange Tests

    /// <summary>
    /// Tests permanently removing multiple entities in a single operation
    /// </summary>
    /// <remarks>
    /// Scenario: Permanently deleting a collection of entities
    /// Expected: All provided entities are completely removed from the database
    /// Coverage: HardRemoveRange method implementation
    /// </remarks>
    [Fact]
    public async Task HardRemoveRange_ShouldPermanentlyRemoveMultipleEntities()
    {
        // Arrange
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Hard Delete Range 1",
                RoleName = RoleType.Customer,
                Email = "harddeleterange@example.com"
            },
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "Hard Delete Range 2",
                RoleName = RoleType.Customer,
                Email = "harddeleterange@example.com"
            }
        };
        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _genericRepository.HardRemoveRange(users);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeTrue();
        var remainingUsers = await _dbContext.Users.Where(u => u.Email == "harddeleterange@example.com").ToListAsync();
        remainingUsers.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync With Includes Tests

    /// <summary>
    /// Tests retrieving an entity with its related entities using navigation properties
    /// </summary>
    /// <remarks>
    /// Scenario: Fetching an entity by ID with eager loading of related entities
    /// Expected: The entity is returned with all related entities loaded
    /// Coverage: GetByIdAsync method with includes parameter
    /// </remarks>
    [Fact]
    public async Task GetByIdAsync_ShouldIncludeRelatedEntities_WhenIncludesProvided()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "User With Address",
            RoleName = RoleType.Customer,
            Email = "withaddress@example.com"
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var address = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine = "123 Test St",
            City = "Test City",
            Country = "Vietnam",
            UserId = user.Id,
            FullName = "Test User",
            Phone = "1234567890"
        };
        await _dbContext.Set<Address>().AddAsync(address);
        await _dbContext.SaveChangesAsync();

        // Act - Use a different repository to test includes
        var userRepository = new GenericRepository<User>(
            _dbContext,
            _timeServiceMock.Object,
            _claimsServiceMock.Object
        );

        var result = await userRepository.GetByIdAsync(user.Id, u => u.Addresses);

        // Assert
        result.Should().NotBeNull();
        result.Addresses.Should().NotBeNull();
        result.Addresses.Should().HaveCount(1);
        result.Addresses.First().AddressLine.Should().Be("123 Test St");
    }

    #endregion

    #region FirstOrDefaultAsync With Includes Tests

    /// <summary>
    /// Tests finding an entity with its related entities using a predicate
    /// </summary>
    /// <remarks>
    /// Scenario: Finding an entity using a predicate with eager loading of related entities
    /// Expected: The first matching entity is returned with all related entities loaded
    /// Coverage: FirstOrDefaultAsync method with includes parameter
    /// </remarks>
    [Fact]
    public async Task FirstOrDefaultAsync_ShouldIncludeRelatedEntities_WhenIncludesProvided()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "First With Address",
            RoleName = RoleType.Customer,
            Email = "firstwithaddress@example.com"
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var address = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine = "456 Test Ave",
            City = "Test City",
            Country = "Vietnam",
            UserId = user.Id,
            FullName = "Test User",
            Phone = "0987654321"
        };
        await _dbContext.Set<Address>().AddAsync(address);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _genericRepository.FirstOrDefaultAsync(
            u => u.Email == "firstwithaddress@example.com",
            u => u.Addresses
        );

        // Assert
        result.Should().NotBeNull();
        result.Addresses.Should().NotBeNull();
        result.Addresses.Should().HaveCount(1);
        result.Addresses.First().AddressLine.Should().Be("456 Test Ave");
    }

    #endregion

    #region Edge Cases Tests

    /// <summary>
    /// Tests retrieving entities with a filter that matches no entities
    /// </summary>
    /// <remarks>
    /// Scenario: Fetching entities with a predicate that doesn't match any entities
    /// Expected: An empty list is returned
    /// Coverage: GetAllAsync edge case handling
    /// </remarks>
    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoEntitiesMatch()
    {
        // Act
        var result = await _genericRepository.GetAllAsync(u => u.Email == "nonexistent@example.com");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Tests hard remove operation when no entities match the predicate
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to permanently delete entities with a predicate that matches nothing
    /// Expected: Method returns false indicating no entities were removed
    /// Coverage: HardRemove method edge case handling
    /// </remarks>
    [Fact]
    public async Task HardRemove_ShouldReturnFalse_WhenNoEntitiesMatch()
    {
        // Act
        var result = await _genericRepository.HardRemove(u => u.Email == "nonexistent@example.com");
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}