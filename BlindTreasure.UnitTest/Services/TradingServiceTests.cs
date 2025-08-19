using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Domain.DTOs.TradeHistoryDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using MockQueryable.Moq;
using Moq;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.ListingDTOs;

namespace BlindTreasure.UnitTest.Services;

public class TradingServiceTests
{
    private readonly TradingService _tradingService;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IListingService> _listingServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IHubContext<NotificationHub>> _notificationHubMock;
    private readonly Mock<IGenericRepository<Listing>> _listingRepoMock;
    private readonly Mock<IGenericRepository<TradeRequest>> _tradeRequestRepoMock;
    private readonly Mock<IGenericRepository<TradeRequestItem>> _tradeRequestItemRepoMock;
    private readonly Mock<IGenericRepository<TradeHistory>> _tradeHistoryRepoMock;
    private readonly Mock<IGenericRepository<InventoryItem>> _inventoryItemRepoMock;
    private readonly Mock<IGenericRepository<User>> _userRepoMock;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public TradingServiceTests()
    {
        _claimsServiceMock = new Mock<IClaimsService>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _listingServiceMock = new Mock<IListingService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _notificationHubMock = new Mock<IHubContext<NotificationHub>>();

        _listingRepoMock = new Mock<IGenericRepository<Listing>>();
        _tradeRequestRepoMock = new Mock<IGenericRepository<TradeRequest>>();
        _tradeRequestItemRepoMock = new Mock<IGenericRepository<TradeRequestItem>>();
        _tradeHistoryRepoMock = new Mock<IGenericRepository<TradeHistory>>();
        _inventoryItemRepoMock = new Mock<IGenericRepository<InventoryItem>>();
        _userRepoMock = new Mock<IGenericRepository<User>>();

        _unitOfWorkMock.Setup(u => u.Listings).Returns(_listingRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TradeRequests).Returns(_tradeRequestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TradeRequestItems).Returns(_tradeRequestItemRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TradeHistories).Returns(_tradeHistoryRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.InventoryItems).Returns(_inventoryItemRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _claimsServiceMock.Setup(c => c.CurrentUserId).Returns(_currentUserId);

        _tradingService = new TradingService(
            _claimsServiceMock.Object,
            _loggerServiceMock.Object,
            _unitOfWorkMock.Object,
            _notificationServiceMock.Object,
            _listingServiceMock.Object,
            _notificationHubMock.Object
        );
    }

    private User CreateTestUser(Guid? id = null, string? email = null, string? fullName = null,
        UserStatus? status = null, RoleType? roleName = null, bool? isEmailVerified = null,
        DateTime? dateOfBirth = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Email = email ?? $"testuser_{Guid.NewGuid()}@example.com",
            Password = new PasswordHasher().HashPassword("Password123!"), // Use a consistent hashed password
            FullName = fullName ?? "Test User",
            Status = status ?? UserStatus.Active,
            RoleName = roleName ?? RoleType.Customer,
            IsEmailVerified = isEmailVerified ?? true,
            DateOfBirth = dateOfBirth ?? new DateTime(2000, 1, 1)
        };
    }

    #region GetTradeRequestsAsync Tests

    /// <summary>
    /// Checks if the method returns a list of trade requests for a valid listing ID.
    /// </summary>
    /// <remarks>
    /// Scenario: A valid listing ID is provided.
    /// Expected: A list of TradeRequestDto associated with the listing is returned.
    /// Coverage: Retrieving trade requests for an existing listing.
    /// </remarks>
    [Fact]
    public async Task GetTradeRequestsAsync_ShouldReturnTradeRequests_WhenListingExists()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var listing = new Listing
        {
            Id = listingId,
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Product = new Product { Name = "Listed Product" }
            }
        };

        var tradeRequests = new List<TradeRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ListingId = listingId,
                RequesterId = requesterId,
                Requester = CreateTestUser(requesterId, fullName: "Requester 1"),
                OfferedItems = new List<TradeRequestItem>()
            }
        };

        var offeredInventoryItem = new InventoryItem
            { Id = Guid.NewGuid(), Product = new Product { Name = "Offered Product" } };

        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);

        _tradeRequestRepoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>(),
                It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequests);

        _inventoryItemRepoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>(),
                It.IsAny<Expression<Func<InventoryItem, object>>[]>()))
            .ReturnsAsync(new List<InventoryItem> { offeredInventoryItem });

        // Act
        var result = await _tradingService.GetTradeRequestsAsync(listingId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().ListingItemName.Should().Be(listing.InventoryItem.Product.Name);
        _listingRepoMock.Verify(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()),
            Times.Once);
        _tradeRequestRepoMock.Verify(r => r.GetAllAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>(),
            It.IsAny<Expression<Func<TradeRequest, object>>[]>()), Times.Once);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to retrieve trade requests for a non-existent listing.
    /// </summary>
    /// <remarks>
    /// Scenario: A non-existent listing ID is provided.
    /// Expected: An Exception with a 404 (Not Found) status code is thrown.
    /// Coverage: Error handling for retrieving trade requests with an invalid listing ID.
    /// </remarks>
    [Fact]
    public async Task GetTradeRequestsAsync_ShouldThrowNotFound_WhenListingDoesNotExist()
    {
        // Arrange
        var listingId = Guid.NewGuid();

        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync((Listing)null!); // Listing does not exist

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _tradingService.GetTradeRequestsAsync(listingId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
        _listingRepoMock.Verify(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()),
            Times.Once);
        _tradeRequestRepoMock.Verify(r => r.GetAllAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>(),
                It.IsAny<Expression<Func<TradeRequest, object>>[]>()),
            Times.Never); // Should not proceed to get trade requests
    }

    #endregion

    #region GetTradeHistoriesAsync Tests

    /// <summary>
    /// Checks if all trade histories are returned when 'onlyMine' is false, with correct filtering and sorting applied.
    /// </summary>
    /// <remarks>
    /// Scenario: Requesting all trade histories without restricting to the current user.
    /// Expected: A paginated list of all relevant trade histories is returned.
    /// Coverage: Comprehensive retrieval of trade histories, including filtering and sorting for public/admin views.
    /// </remarks>
    [Fact]
    public async Task GetTradeHistoriesAsync_ShouldReturnAllTradeHistories_WhenOnlyMineIsFalse()
    {
        // Arrange
        var param = new TradeHistoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10,
            FinalStatus = TradeRequestStatus.COMPLETED, // Example filter
            SortBy = "CompletedAt",
            Desc = true
        };

        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var listingId1 = Guid.NewGuid();
        var listingId2 = Guid.NewGuid();

        var tradeHistories = new List<TradeHistory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ListingId = listingId1,
                RequesterId = userId1,
                FinalStatus = TradeRequestStatus.COMPLETED,
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product A" } } },
                Requester = CreateTestUser(userId1, fullName: "User One", dateOfBirth: new DateTime(1990, 1, 1))
            },
            new()
            {
                Id = Guid.NewGuid(),
                ListingId = listingId2,
                RequesterId = userId2,
                FinalStatus = TradeRequestStatus.COMPLETED,
                CompletedAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product B" } } },
                Requester = CreateTestUser(userId2, fullName: "User Two", dateOfBirth: new DateTime(1991, 1, 1))
            },
            new()
            {
                Id = Guid.NewGuid(),
                ListingId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                FinalStatus = TradeRequestStatus.REJECTED,
                CompletedAt = DateTime.UtcNow.AddDays(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product C" } } },
                Requester = CreateTestUser(fullName: "User Three", dateOfBirth: new DateTime(1992, 1, 1))
            }
        }.AsQueryable().BuildMock();

        _tradeHistoryRepoMock.Setup(r => r.GetQueryable()).Returns(tradeHistories);

        // Act
        var result = await _tradingService.GetTradeHistoriesAsync(param, false);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2); // Only COMPLETED status should be returned
        result.Should().HaveCount(2);
        result.First().ListingItemName.Should().Be("Product A"); // Ordered by CompletedAt Desc
        result.Last().ListingItemName.Should().Be("Product B");

        _loggerServiceMock.Verify(x => x.Info(It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Checks if only trade histories for the current user are returned when 'onlyMine' is true.
    /// </summary>
    /// <remarks>
    /// Scenario: Requesting trade histories with 'onlyMine' set to true.
    /// Expected: A paginated list containing only trade histories where the current user is the requester.
    /// Coverage: Filtering trade histories by the current user's ID.
    /// </remarks>
    [Fact]
    public async Task GetTradeHistoriesAsync_ShouldReturnUserTradeHistories_WhenOnlyMineIsTrue()
    {
        // Arrange
        var param = new TradeHistoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10
        };

        var tradeHistories = new List<TradeHistory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequesterId = _currentUserId,
                IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "My Product" } } },
                Requester = CreateTestUser(_currentUserId, fullName: "Me", dateOfBirth: new DateTime(1995, 1, 1))
            },
            new()
            {
                Id = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(), // Different user
                IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Other Product" } } },
                Requester = CreateTestUser(fullName: "Other", dateOfBirth: new DateTime(1996, 1, 1))
            }
        }.AsQueryable().BuildMock();

        _tradeHistoryRepoMock.Setup(r => r.GetQueryable()).Returns(tradeHistories);

        // Act
        var result = await _tradingService.GetTradeHistoriesAsync(param, true);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Should().HaveCount(1);
        result.First().RequesterId.Should().Be(_currentUserId);
        _loggerServiceMock.Verify(x => x.Info(It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Checks if filtering by FinalStatus works correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: Requesting trade histories filtered by a specific FinalStatus.
    /// Expected: Only trade histories matching the specified status are returned.
    /// Coverage: Filtering capabilities by trade history final status.
    /// </remarks>
    [Fact]
    public async Task GetTradeHistoriesAsync_ShouldFilterByFinalStatus()
    {
        // Arrange
        var param = new TradeHistoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10,
            FinalStatus = TradeRequestStatus.ACCEPTED
        };

        var tradeHistories = new List<TradeHistory>
        {
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.ACCEPTED,
                IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product Accepted" } } },
                Requester = CreateTestUser(_currentUserId, fullName: "User Accepted",
                    dateOfBirth: new DateTime(1990, 1, 1))
            },
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.REJECTED,
                IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product Rejected" } } },
                Requester = CreateTestUser(_currentUserId, fullName: "User Rejected",
                    dateOfBirth: new DateTime(1990, 1, 1))
            }
        }.AsQueryable().BuildMock();

        _tradeHistoryRepoMock.Setup(r => r.GetQueryable()).Returns(tradeHistories);

        // Act
        var result = await _tradingService.GetTradeHistoriesAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Should().HaveCount(1);
        result.First().FinalStatus.Should().Be(TradeRequestStatus.ACCEPTED);
    }

    /// <summary>
    /// Checks if filtering by ListingId works correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: Requesting trade histories filtered by a specific ListingId.
    /// Expected: Only trade histories associated with the specified listing are returned.
    /// Coverage: Filtering capabilities by listing ID.
    /// </remarks>
    [Fact]
    public async Task GetTradeHistoriesAsync_ShouldFilterByListingId()
    {
        // Arrange
        var specificListingId = Guid.NewGuid();
        var param = new TradeHistoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10,
            ListingId = specificListingId
        };

        var tradeHistories = new List<TradeHistory>
        {
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                ListingId = specificListingId, IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product for Listing" } } },
                Requester = CreateTestUser(fullName: "User Listing")
            },
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                ListingId = Guid.NewGuid(), // Different listing
                IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Other Listing Product" } } },
                Requester = CreateTestUser(fullName: "User Other Listing")
            }
        }.AsQueryable().BuildMock();

        _tradeHistoryRepoMock.Setup(r => r.GetQueryable()).Returns(tradeHistories);

        // Act
        var result = await _tradingService.GetTradeHistoriesAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Should().HaveCount(1);
        result.First().ListingId.Should().Be(specificListingId);
    }

    /// <summary>
    /// Checks if filtering by date ranges (CompletedAt, CreatedAt) works correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: Requesting trade histories filtered by a combination of date ranges.
    /// Expected: Only trade histories falling within the specified date ranges are returned.
    /// Coverage: Date range filtering for trade history inquiries.
    /// </remarks>
    [Fact]
    public async Task GetTradeHistoriesAsync_ShouldFilterByDateRanges()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(-3);
        var toDate = DateTime.UtcNow.AddDays(-1);

        var param = new TradeHistoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10,
            CompletedFromDate = fromDate,
            CompletedToDate = toDate,
            CreatedFromDate = fromDate.AddDays(-1),
            CreatedToDate = toDate.AddDays(1)
        };

        var tradeHistories = new List<TradeHistory>
        {
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                CompletedAt = DateTime.UtcNow.AddDays(-2), CreatedAt = DateTime.UtcNow.AddDays(-3),
                IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product In Range" } } },
                Requester = CreateTestUser(fullName: "User In Range")
            },
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                CompletedAt = DateTime.UtcNow.AddDays(-5), CreatedAt = DateTime.UtcNow.AddDays(-6),
                IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product Out Of Range" } } },
                Requester = CreateTestUser(fullName: "User Out Of Range")
            }
        }.AsQueryable().BuildMock();

        _tradeHistoryRepoMock.Setup(r => r.GetQueryable()).Returns(tradeHistories);

        // Act
        var result = await _tradingService.GetTradeHistoriesAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Should().HaveCount(1);
        result.First().ListingItemName.Should().Be("Product In Range");
    }

    /// <summary>
    /// Checks if the default sorting by CompletedAt in descending order works correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: Requesting trade histories without specifying a sort order.
    /// Expected: Trade histories are sorted by CompletedAt in descending order by default.
    /// Coverage: Default sorting behavior of trade histories.
    /// </remarks>
    [Fact]
    public async Task GetTradeHistoriesAsync_ShouldSortByCompletedAtDescendingByDefault()
    {
        // Arrange
        var param = new TradeHistoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10
            // No SortBy specified
        };

        var tradeHistories = new List<TradeHistory>
        {
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                CompletedAt = DateTime.UtcNow.AddDays(-1), IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product Latest" } } },
                Requester = CreateTestUser(fullName: "User Latest")
            },
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                CompletedAt = DateTime.UtcNow.AddDays(-2), IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product Earlier" } } },
                Requester = CreateTestUser(fullName: "User Earlier")
            }
        }.AsQueryable().BuildMock();

        _tradeHistoryRepoMock.Setup(r => r.GetQueryable()).Returns(tradeHistories);

        // Act
        var result = await _tradingService.GetTradeHistoriesAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Should().HaveCount(2);
        result.First().ListingItemName.Should().Be("Product Latest");
        result.Last().ListingItemName.Should().Be("Product Earlier");
    }

    /// <summary>
    /// Checks if sorting by CreatedAt in ascending order works correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: Requesting trade histories sorted by CreatedAt in ascending order.
    /// Expected: Trade histories are sorted by their creation date from oldest to newest.
    /// Coverage: Explicit sorting by creation date.
    /// </remarks>
    [Fact]
    public async Task GetTradeHistoriesAsync_ShouldSortByCreatedAtAscending()
    {
        // Arrange
        var param = new TradeHistoryQueryParameter
        {
            PageIndex = 1,
            PageSize = 10,
            SortBy = "CreatedAt",
            Desc = false // Ascending
        };

        var tradeHistories = new List<TradeHistory>
        {
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                CreatedAt = DateTime.UtcNow.AddDays(-1), IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product Newer" } } },
                Requester = CreateTestUser(fullName: "User Newer")
            },
            new()
            {
                Id = Guid.NewGuid(), RequesterId = _currentUserId, FinalStatus = TradeRequestStatus.COMPLETED,
                CreatedAt = DateTime.UtcNow.AddDays(-2), IsDeleted = false,
                Listing = new Listing
                    { InventoryItem = new InventoryItem { Product = new Product { Name = "Product Older" } } },
                Requester = CreateTestUser(fullName: "User Older")
            }
        }.AsQueryable().BuildMock();

        _tradeHistoryRepoMock.Setup(r => r.GetQueryable()).Returns(tradeHistories);

        // Act
        var result = await _tradingService.GetTradeHistoriesAsync(param);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Should().HaveCount(2);
        result.First().ListingItemName.Should().Be("Product Older");
        result.Last().ListingItemName.Should().Be("Product Newer");
    }

    #endregion

    #region CreateTradeRequestAsync Tests

    /// <summary>
    /// Checks if a 'Not Found' error occurs when the listing for a trade request does not exist.
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to create a trade request for a non-existent listing.
    /// Expected: An Exception with a 404 (Not Found) status code is thrown.
    /// Coverage: Error handling for trade request creation with an invalid listing ID.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowNotFound_WhenListingNotExists()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto();

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ThrowsAsync(ErrorHelper.NotFound("Listing không tồn tại"));

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when the listing for a trade request is not active.
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to create a trade request for a listing that is not in 'Active' status.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Validation of listing status during trade request creation.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenListingNotActive()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto();

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Sold });

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Sold, // Not Active
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Product = new Product()
            }
        };
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when a user tries to create a trade request for their own listing.
    /// </summary>
    /// <remarks>
    /// Scenario: The current user attempts to create a trade request for a listing whose underlying inventory item is owned by them.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Preventing users from trading with their own items.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenUserIsListingOwner()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto();

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Active,
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = _currentUserId, // Owned by current user
                Product = new Product()
            }
        };

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Active });
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when the listing's inventory item is on hold.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to create a trade request for a listing whose inventory item is currently on hold.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Validation of inventory item status for tradeability.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenListingItemIsOnHold()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto();

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Active,
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Status = InventoryItemStatus.OnHold,
                HoldUntil = DateTime.UtcNow.AddDays(1), // Still on hold
                Product = new Product()
            }
        };

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Active });
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when a pending trade request already exists for the same user and listing.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to create a new trade request for a listing for which they already have a pending request.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Preventing duplicate pending trade requests.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenPendingTradeRequestExists()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto();

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Active,
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Product = new Product()
            }
        };

        var existingTradeRequest = new TradeRequest
            { ListingId = listingId, RequesterId = _currentUserId, Status = TradeRequestStatus.PENDING };

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Active });
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);
        _tradeRequestRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>()))
            .ReturnsAsync(existingTradeRequest);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when the offered items contain duplicates.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to create a trade request by offering the same inventory item multiple times.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Validating offered items for duplicates.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenOfferedItemsContainDuplicates()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var offeredItemId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto
        {
            OfferedInventoryIds = new List<Guid> { offeredItemId, offeredItemId } // Duplicate
        };

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Active,
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Product = new Product(),
                Status = InventoryItemStatus.Available
            }
        };

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Active });
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);
        _tradeRequestRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>()))
            .ReturnsAsync((TradeRequest)null!); // No existing pending trade request

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        ExceptionUtils.ExtractMessage(exception).Should().Contain("không thể đề xuất cùng một vật phẩm nhiều lần");
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when offered items are not owned by the current user.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to create a trade request offering an item that they do not own.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Validating ownership of offered items.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenOfferedItemsNotOwnedByUser()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var offeredItemId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto
        {
            OfferedInventoryIds = new List<Guid> { offeredItemId }
        };

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Active,
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Product = new Product(),
                Status = InventoryItemStatus.Available
            }
        };

        var offeredItem = new InventoryItem
        {
            Id = offeredItemId,
            UserId = Guid.NewGuid(), // Not owned by current user
            Status = InventoryItemStatus.Available,
            Product = new Product { Name = "Other's Item" }
        };

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Active });
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);
        _tradeRequestRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>()))
            .ReturnsAsync((TradeRequest)null!); // No existing pending trade request

        _inventoryItemRepoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>(),
                It.IsAny<Expression<Func<InventoryItem, object>>[]>()))
            .ReturnsAsync(new List<InventoryItem> { offeredItem });

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        ExceptionUtils.ExtractMessage(exception).Should().Contain("không thuộc sở hữu của bạn");
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when offered items are not available (e.g., on hold, sold).
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to create a trade request by offering an item that is not in 'Available' status.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Validating the availability status of offered items.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenOfferedItemsAreNotAvailable()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var offeredItemId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto
        {
            OfferedInventoryIds = new List<Guid> { offeredItemId }
        };

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Active,
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Product = new Product(),
                Status = InventoryItemStatus.Available
            }
        };

        var offeredItem = new InventoryItem
        {
            Id = offeredItemId,
            UserId = _currentUserId,
            Status = InventoryItemStatus.OnHold,
            HoldUntil = DateTime.UtcNow.AddDays(1),
            Product = new Product { Name = "On Hold Item" }
        };

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Active });
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);
        _tradeRequestRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>()))
            .ReturnsAsync((TradeRequest)null!); // No existing pending trade request

        _inventoryItemRepoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>(),
                It.IsAny<Expression<Func<InventoryItem, object>>[]>()))
            .ReturnsAsync(new List<InventoryItem> { offeredItem });

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        ExceptionUtils.ExtractMessage(exception).Should().Contain("hiện không khả dụng để trao đổi");
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when no offered items are provided for a non-free listing.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to create a trade request for a non-free listing without offering any items.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Validation for requiring offered items on non-free listings.
    /// </remarks>
    [Fact]
    public async Task CreateTradeRequestAsync_ShouldThrowBadRequest_WhenNoOfferedItemsForNonFreeListing()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var requestDto = new CreateTradeRequestDto
        {
            OfferedInventoryIds = new List<Guid>() // No offered items
        };

        var listing = new Listing
        {
            Id = listingId,
            Status = ListingStatus.Active,
            IsFree = false, // Not free
            InventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Product = new Product(),
                Status = InventoryItemStatus.Available
            }
        };

        _listingServiceMock.Setup(s => s.GetListingByIdAsync(listingId))
            .ReturnsAsync(new ListingDetailDto { Id = listingId, Status = ListingStatus.Active });
        _listingRepoMock.Setup(r => r.GetByIdAsync(listingId, It.IsAny<Expression<Func<Listing, object>>[]>()))
            .ReturnsAsync(listing);
        _tradeRequestRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TradeRequest, bool>>>()))
            .ReturnsAsync((TradeRequest)null!); // No existing pending trade request

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.CreateTradeRequestAsync(listingId, requestDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        ExceptionUtils.ExtractMessage(exception).Should().Contain("phải đề xuất ít nhất một vật phẩm");
        _tradeRequestRepoMock.Verify(r => r.AddAsync(It.IsAny<TradeRequest>()), Times.Never);
    }

    #endregion

    #region RespondTradeRequestAsync Tests

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to respond to a non-existent trade request.
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to respond to a trade request using an ID that does not exist.
    /// Expected: An Exception with a 404 (Not Found) status code is thrown.
    /// Coverage: Error handling for responding to non-existent trade requests.
    /// </remarks>
    [Fact]
    public async Task RespondTradeRequestAsync_ShouldThrowNotFound_WhenTradeRequestDoesNotExist()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync((TradeRequest)null!); // Trade request does not exist

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.RespondTradeRequestAsync(tradeRequestId, true));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
        _tradeRequestRepoMock.Verify(r => r.Update(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when trying to respond to a trade request that has already been processed (not PENDING).
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to respond to a trade request that is already ACCEPTED or REJECTED.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Preventing multiple responses to the same trade request.
    /// </remarks>
    [Fact]
    public async Task RespondTradeRequestAsync_ShouldThrowBadRequest_WhenTradeRequestAlreadyProcessed()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingOwnerId = _currentUserId;
        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            Status = TradeRequestStatus.ACCEPTED, // Already processed
            Listing = new Listing
            {
                InventoryItem = new InventoryItem { UserId = listingOwnerId, Product = new Product() }
            }
        };

        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.RespondTradeRequestAsync(tradeRequestId, true));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        _tradeRequestRepoMock.Verify(r => r.Update(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Forbidden' error occurs when a user who is not the listing owner tries to respond to a trade request.
    /// </summary>
    /// <remarks>
    /// Scenario: A user, who is not the owner of the listing item, attempts to accept or reject a trade request.
    /// Expected: An Exception with a 403 (Forbidden) status code is thrown.
    /// Coverage: Authorization check for responding to trade requests.
    /// </remarks>
    [Fact]
    public async Task RespondTradeRequestAsync_ShouldThrowForbidden_WhenUserIsNotOwner()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingOwnerId = Guid.NewGuid(); // Different from _currentUserId
        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            Status = TradeRequestStatus.PENDING,
            Listing = new Listing
            {
                InventoryItem = new InventoryItem { UserId = listingOwnerId, Product = new Product() }
            }
        };

        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.RespondTradeRequestAsync(tradeRequestId, true));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(403);
        _tradeRequestRepoMock.Verify(r => r.Update(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Checks if the listing item's status is restored to Available when a trade request is rejected.
    /// </summary>
    /// <remarks>
    /// Scenario: A trade request is rejected, and the listing item was previously on hold due to the trade request.
    /// Expected: The listing item's status is updated to InventoryItemStatus.Available.
    /// Coverage: Correct status management of listing items upon trade request rejection.
    /// </remarks>
    [Fact]
    public async Task RespondTradeRequestAsync_ShouldRestoreListingItemStatus_WhenRejected()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingId = Guid.NewGuid();
        var listingOwnerId = _currentUserId;
        var listingItemId = Guid.NewGuid();

        var listingItem = new InventoryItem
        {
            Id = listingItemId,
            UserId = listingOwnerId,
            Status = InventoryItemStatus.OnHold, // On hold due to trade request
            LockedByRequestId = tradeRequestId,
            Product = new Product { Name = "Held Product" }
        };

        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            ListingId = listingId,
            RequesterId = Guid.NewGuid(),
            Status = TradeRequestStatus.PENDING,
            Listing = new Listing
            {
                Id = listingId,
                InventoryItem = listingItem
            }
        };

        _tradeRequestRepoMock.Setup(r => r.GetByIdAsync(tradeRequestId,
                It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);
        _tradeRequestRepoMock.Setup(r => r.Update(It.IsAny<TradeRequest>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _userRepoMock.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User
        {
            Id = listingOwnerId,
            Email = "test@example.com",
            IsDeleted = false,
            RoleName = RoleType.Customer,
            FullName = "Old Name"
        });

        // Mock GetTradeRequestByIdAsync call at the end
        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        _inventoryItemRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<InventoryItem>().AsQueryable().BuildMock());

        // Act
        var result = await _tradingService.RespondTradeRequestAsync(tradeRequestId, false); // Reject

        // Assert
        result.Should().NotBeNull();
        tradeRequest.Listing.InventoryItem.Status.Should().Be(InventoryItemStatus.Available);
        tradeRequest.Listing.InventoryItem.LockedByRequestId.Should().BeNull();
        _inventoryItemRepoMock.Verify(r => r.Update(listingItem), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if the listing item's status is NOT restored to Available when a trade request is accepted.
    /// </summary>
    /// <remarks>
    /// Scenario: A trade request is accepted. The listing item's status should remain on hold or be further processed, not reverted to Available prematurely.
    /// Expected: The listing item's status remains unchanged by this specific operation, or is set to a state appropriate for ongoing trade.
    /// Coverage: Ensuring correct status management of listing items upon trade request acceptance (i.e., not releasing hold).
    /// </remarks>
    [Fact]
    public async Task RespondTradeRequestAsync_ShouldNotRestoreListingItemStatus_WhenAccepted()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingId = Guid.NewGuid();
        var listingOwnerId = _currentUserId;
        var listingItemId = Guid.NewGuid();

        var listingItem = new InventoryItem
        {
            Id = listingItemId,
            UserId = listingOwnerId,
            Status = InventoryItemStatus.OnHold, // On hold due to trade request
            LockedByRequestId = tradeRequestId,
            Product = new Product { Name = "Held Product" }
        };

        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            ListingId = listingId,
            RequesterId = Guid.NewGuid(),
            Status = TradeRequestStatus.PENDING,
            Listing = new Listing
            {
                Id = listingId,
                InventoryItem = listingItem
            }
        };

        _tradeRequestRepoMock.Setup(r => r.GetByIdAsync(tradeRequestId,
                It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);
        _tradeRequestRepoMock.Setup(r => r.Update(It.IsAny<TradeRequest>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _userRepoMock.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User
        {
            Id = listingOwnerId,
            Email = "test@example.com",
            IsDeleted = false,
            RoleName = RoleType.Customer,
            FullName = "Old Name"
        });

        // Mock GetTradeRequestByIdAsync call at the end
        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        _inventoryItemRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<InventoryItem>().AsQueryable().BuildMock());

        // Act
        var result = await _tradingService.RespondTradeRequestAsync(tradeRequestId, true); // Accept

        // Assert
        result.Should().NotBeNull();
        // The status should remain OnHold or be handled by subsequent logic (LockDealAsync), not revert to Available here.
        tradeRequest.Listing.InventoryItem.Status.Should().Be(InventoryItemStatus.OnHold); // Status remains OnHold
        _inventoryItemRepoMock.Verify(r => r.Update(It.IsAny<InventoryItem>()),
            Times.Never); // Should not call update on listing item here
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region LockDealAsync Tests

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to lock a non-existent trade request.
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to lock a trade request using an ID that does not exist.
    /// Expected: An Exception with a 404 (Not Found) status code is thrown.
    /// Coverage: Error handling for locking non-existent trade requests.
    /// </remarks>
    [Fact]
    public async Task LockDealAsync_ShouldThrowNotFound_WhenTradeRequestDoesNotExist()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync((TradeRequest)null!); // Trade request does not exist

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _tradingService.LockDealAsync(tradeRequestId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
        _tradeRequestRepoMock.Verify(r => r.Update(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when trying to lock a trade request that is not in the ACCEPTED status.
    /// </summary>
    /// <remarks>
    /// Scenario: Attempting to lock a trade request that is still PENDING or already COMPLETED/REJECTED.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Validating trade request status before allowing a lock.
    /// </remarks>
    [Fact]
    public async Task LockDealAsync_ShouldThrowBadRequest_WhenTradeRequestNotAccepted()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            Status = TradeRequestStatus.PENDING, // Not Accepted
            Listing = new Listing
            {
                InventoryItem = new InventoryItem { UserId = _currentUserId, Product = new Product() }
            }
        };

        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _tradingService.LockDealAsync(tradeRequestId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        _tradeRequestRepoMock.Verify(r => r.Update(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Forbidden' error occurs when a user who is neither the listing owner nor the requester tries to lock a deal.
    /// </summary>
    /// <remarks>
    /// Scenario: A third-party user attempts to lock an accepted trade request.
    /// Expected: An Exception with a 403 (Forbidden) status code is thrown.
    /// Coverage: Authorization check for locking trade deals.
    /// </remarks>
    [Fact]
    public async Task LockDealAsync_ShouldThrowForbidden_WhenUserIsNotOwnerOrRequester()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingOwnerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var unauthorizedUserId = Guid.NewGuid(); // Neither owner nor requester

        _claimsServiceMock.Setup(c => c.CurrentUserId).Returns(unauthorizedUserId);

        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            Status = TradeRequestStatus.ACCEPTED,
            Listing = new Listing
            {
                InventoryItem = new InventoryItem { UserId = listingOwnerId, Product = new Product() }
            },
            Requester = CreateTestUser(requesterId, fullName: "Requester")
        };

        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _tradingService.LockDealAsync(tradeRequestId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(403);
        _tradeRequestRepoMock.Verify(r => r.Update(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when the owner attempts to lock a deal they have already locked.
    /// </summary>
    /// <remarks>
    /// Scenario: The listing owner attempts to lock an accepted trade request again after already locking it.
    /// Expected: An Exception with a 400 (Bad Request) status code is thrown.
    /// Coverage: Preventing redundant lock actions by the owner.
    /// </remarks>
    [Fact]
    public async Task LockDealAsync_ShouldThrowBadRequest_WhenOwnerAlreadyLocked()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingOwnerId = _currentUserId;

        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            Status = TradeRequestStatus.ACCEPTED,
            OwnerLocked = true, // Already locked by owner
            RequesterLocked = false,
            Listing = new Listing
            {
                InventoryItem = new InventoryItem { UserId = listingOwnerId, Product = new Product() }
            }
        };

        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _tradingService.LockDealAsync(tradeRequestId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
        ExceptionUtils.ExtractMessage(exception).Should().Contain("Bạn đã khóa giao dịch này");
        _tradeRequestRepoMock.Verify(r => r.Update(It.IsAny<TradeRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    #endregion

    #region GetTradeRequestByIdAsync Tests

    /// <summary>
    /// Checks if a TradeRequestDto is returned when a valid trade request ID is provided.
    /// </summary>
    /// <remarks>
    /// Scenario: A valid trade request ID is provided to retrieve its details.
    /// Expected: A TradeRequestDto populated with the request's details, including listing and offered items, is returned.
    /// Coverage: Successful retrieval and mapping of a trade request by ID.
    /// </remarks>
    [Fact]
    public async Task GetTradeRequestByIdAsync_ShouldReturnTradeRequest_WhenFound()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var listingOwnerId = Guid.NewGuid();
        var offeredItemId = Guid.NewGuid();

        var listingItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            UserId = listingOwnerId,
            Product = new Product { Name = "Listed Product", ImageUrls = new List<string> { "listing.jpg" } },
            Tier = RarityName.Common
        };

        var offeredItem = new InventoryItem
        {
            Id = offeredItemId,
            UserId = requesterId,
            Product = new Product { Name = "Offered Product", ImageUrls = new List<string> { "offered.jpg" } },
            Tier = RarityName.Rare
        };

        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            ListingId = listingId,
            RequesterId = requesterId,
            Status = TradeRequestStatus.PENDING,
            RequestedAt = DateTime.UtcNow,
            Listing = new Listing
            {
                Id = listingId,
                InventoryItem = listingItem
            },
            OfferedItems = new List<TradeRequestItem> { new() { InventoryItemId = offeredItemId } },
            Requester = CreateTestUser(requesterId, fullName: "Requester Name")
        };

        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        _inventoryItemRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<InventoryItem> { offeredItem }.AsQueryable().BuildMock());

        // Mock for MapTradeRequestToDto's internal calls (Users for FullName, AvatarUrl etc.)
        _userRepoMock.Setup(u => u.GetByIdAsync(listingOwnerId))
            .ReturnsAsync(CreateTestUser(listingOwnerId, fullName: "Listing Owner", email: "owner@example.com"));
        _userRepoMock.Setup(u => u.GetByIdAsync(requesterId))
            .ReturnsAsync(CreateTestUser(requesterId, fullName: "Requester Name", email: "requester@example.com"));

        // Act
        var result = await _tradingService.GetTradeRequestByIdAsync(tradeRequestId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(tradeRequestId);
        result.ListingId.Should().Be(listingId);
        result.RequesterId.Should().Be(requesterId);
        result.Status.Should().Be(TradeRequestStatus.PENDING);
        result.ListingItemName.Should().Be("Listed Product");
        result.OfferedItems.Should().ContainSingle(item =>
            item.InventoryItemId == offeredItemId && item.ItemName == "Offered Product");

        _tradeRequestRepoMock.Verify(
            r => r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()), Times.Once);
        _inventoryItemRepoMock.Verify(r => r.GetQueryable(), Times.Once);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to retrieve a non-existent trade request by ID.
    /// </summary>
    /// <remarks>
    /// Scenario: A non-existent trade request ID is provided.
    /// Expected: An Exception with a 404 (Not Found) status code is thrown.
    /// Coverage: Error handling for retrieving a trade request with an invalid ID.
    /// </remarks>
    [Fact]
    public async Task GetTradeRequestByIdAsync_ShouldThrowNotFound_WhenNotFound()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync((TradeRequest)null!); // Trade request does not exist

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _tradingService.GetTradeRequestByIdAsync(tradeRequestId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
        _tradeRequestRepoMock.Verify(
            r => r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()), Times.Once);
    }

    /// <summary>
    /// Checks if offered items are correctly mapped and included in the returned DTO.
    /// </summary>
    /// <remarks>
    /// Scenario: A trade request has multiple offered items.
    /// Expected: The returned TradeRequestDto correctly lists all offered items with their details.
    /// Coverage: Comprehensive mapping of offered items within a trade request.
    /// </remarks>
    [Fact]
    public async Task GetTradeRequestByIdAsync_ShouldMapOfferedItemsCorrectly()
    {
        // Arrange
        var tradeRequestId = Guid.NewGuid();
        var listingId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var listingOwnerId = Guid.NewGuid();
        var offeredItemId1 = Guid.NewGuid();
        var offeredItemId2 = Guid.NewGuid();

        var listingItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            UserId = listingOwnerId,
            Product = new Product { Name = "Listed Item", ImageUrls = new List<string> { "listing.jpg" } },
            Tier = RarityName.Common
        };

        var offeredItem1 = new InventoryItem
        {
            Id = offeredItemId1,
            UserId = requesterId,
            Product = new Product { Name = "Offered Item 1", ImageUrls = new List<string> { "offered1.jpg" } },
            Tier = RarityName.Rare
        };
        var offeredItem2 = new InventoryItem
        {
            Id = offeredItemId2,
            UserId = requesterId,
            Product = new Product { Name = "Offered Item 2", ImageUrls = new List<string> { "offered2.jpg" } },
            Tier = RarityName.Secret
        };

        var tradeRequest = new TradeRequest
        {
            Id = tradeRequestId,
            ListingId = listingId,
            RequesterId = requesterId,
            Status = TradeRequestStatus.PENDING,
            RequestedAt = DateTime.UtcNow,
            Listing = new Listing
            {
                Id = listingId,
                InventoryItem = listingItem
            },
            OfferedItems = new List<TradeRequestItem>
            {
                new() { InventoryItemId = offeredItemId1 },
                new() { InventoryItemId = offeredItemId2 }
            },
            Requester = CreateTestUser(requesterId, fullName: "Requester Name")
        };

        _tradeRequestRepoMock.Setup(r =>
                r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()))
            .ReturnsAsync(tradeRequest);

        _inventoryItemRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<InventoryItem> { offeredItem1, offeredItem2 }.AsQueryable().BuildMock());

        _userRepoMock.Setup(u => u.GetByIdAsync(listingOwnerId))
            .ReturnsAsync(CreateTestUser(listingOwnerId, fullName: "Listing Owner", email: "owner@example.com"));
        _userRepoMock.Setup(u => u.GetByIdAsync(requesterId))
            .ReturnsAsync(CreateTestUser(requesterId, fullName: "Requester Name", email: "requester@example.com"));

        // Act
        var result = await _tradingService.GetTradeRequestByIdAsync(tradeRequestId);

        // Assert
        result.Should().NotBeNull();
        result.OfferedItems.Should().HaveCount(2);
        result.OfferedItems.Should()
            .Contain(item => item.InventoryItemId == offeredItemId1 && item.ItemName == "Offered Item 1");
        result.OfferedItems.Should()
            .Contain(item => item.InventoryItemId == offeredItemId2 && item.ItemName == "Offered Item 2");

        _tradeRequestRepoMock.Verify(
            r => r.GetByIdAsync(tradeRequestId, It.IsAny<Expression<Func<TradeRequest, object>>[]>()), Times.Once);
        _inventoryItemRepoMock.Verify(r => r.GetQueryable(), Times.Once);
    }

    #endregion
}