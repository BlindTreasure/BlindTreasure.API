using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MockQueryable.Moq;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlindTreasure.Application.Interfaces.Commons;

namespace BlindTreasure.UnitTest.Services;

public class UserServiceTests
{
    private readonly Mock<IBlobService> _blobServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<User>> _userRepoMock;
    private readonly Mock<IGenericRepository<Seller>> _sellerRepoMock;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _blobServiceMock = new Mock<IBlobService>();
        _userRepoMock = new Mock<IGenericRepository<User>>();
        _sellerRepoMock = new Mock<IGenericRepository<Seller>>();

        _unitOfWorkMock.Setup(x => x.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Sellers).Returns(_sellerRepoMock.Object);

        _userService = new UserService(
            _unitOfWorkMock.Object,
            _loggerServiceMock.Object,
            _cacheServiceMock.Object,
            _blobServiceMock.Object
        );
    }


    #region UpdateProfileAsync Tests

    /// <summary>
    /// Tests if a user's profile can be updated successfully with valid data.
    /// </summary>
    /// <remarks>
    /// Scenario: A user provides valid information to update their profile (e.g., full name, phone number).
    /// Expected: The user's profile is updated in the database, and the updated user details are returned.
    /// Coverage: Basic profile update functionality.
    /// </remarks>
    [Fact]
    public async Task UpdateProfileAsync_ShouldUpdateProfile_WhenValidData()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsDeleted = false,
            RoleName = RoleType.Customer,
            FullName = "Old Name"
        };
        var dto = new UpdateProfileDto { FullName = "New Name", PhoneNumber = "123456789" };
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _userRepoMock.Setup(x => x.Update(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        var result = await _userService.UpdateProfileAsync(userId, dto);
        result.Should().NotBeNull();
        result.FullName.Should().Be("New Name");
    }

    /// <summary>
    /// Tests if an exception is thrown when attempting to update a profile for a user that does not exist.
    /// </summary>
    /// <remarks>
    /// Scenario: An attempt is made to update a profile for a user ID that does not exist in the system.
    /// Expected: An `Exception` is thrown, indicating that the user was not found.
    /// Coverage: Error handling for updating a non-existent user's profile.
    /// </remarks>
    [Fact]
    public async Task UpdateProfileAsync_ShouldThrowNotFound_WhenUserNotExists()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User)null!);

        await Assert.ThrowsAsync<Exception>(() => _userService.UpdateProfileAsync(userId, new UpdateProfileDto()));
    }

    #endregion

    #region UploadAvatarAsync Tests

    /// <summary>
    /// Tests if a user's avatar can be uploaded successfully with a valid file.
    /// </summary>
    /// <remarks>
    /// Scenario: A user provides a valid image file to upload as their avatar.
    /// Expected: The avatar is uploaded to blob storage, the user's avatar URL is updated in the database, and the new avatar URL is returned.
    /// Coverage: Avatar upload functionality and integration with blob storage.
    /// </remarks>
    [Fact]
    public async Task UploadAvatarAsync_ShouldUploadAvatar_WhenValidFile()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsDeleted = false,
            RoleName = RoleType.Customer,
            FullName = "Test User"
        };
        var file = CreateMockFormFile();
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _blobServiceMock.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);
        _blobServiceMock.Setup(x => x.GetPreviewUrlAsync(It.IsAny<string>())).ReturnsAsync("http://avatar.url");
        _userRepoMock.Setup(x => x.Update(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        var result = await _userService.UploadAvatarAsync(userId, file);
        result.Should().NotBeNull();
        result.AvatarUrl.Should().Be("http://avatar.url");
    }

    /// <summary>
    /// Tests if a `BadRequest` exception is thrown when attempting to upload an empty avatar file.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to upload an empty file (e.g., 0 bytes) as their avatar.
    /// Expected: An `Exception` is thrown, indicating a bad request due to the empty file.
    /// Coverage: Input validation for avatar file uploads (empty file check).
    /// </remarks>
    [Fact]
    public async Task UploadAvatarAsync_ShouldThrowBadRequest_WhenFileIsEmpty()
    {
        var userId = Guid.NewGuid();
        var user = new User
            { Id = userId, Email = "test@example.com", IsDeleted = false, RoleName = RoleType.Customer };
        var file = new FormFile(new MemoryStream(), 0, 0, "Data", "empty.jpg");
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        await Assert.ThrowsAsync<Exception>(() => _userService.UploadAvatarAsync(userId, file));
    }

    #endregion

    #region CreateUserAsync Tests

    /// <summary>
    /// Tests if a new user can be created successfully when the provided email does not already exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A new user attempts to register with an email address that is not already in the system.
    /// Expected: The user is successfully created in the database, and the new user details are returned.
    /// Coverage: User creation functionality and email uniqueness check.
    /// </remarks>
    [Fact]
    public async Task CreateUserAsync_ShouldCreateUser_WhenEmailNotExists()
    {
        var dto = new UserCreateDto
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            FullName = "New User",
            RoleName = RoleType.Customer
        };
        _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>())).ReturnsAsync((User)null!);
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((User)null!);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = dto.Email, RoleName = dto.RoleName });
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _userService.CreateUserAsync(dto);

        result.Should().NotBeNull();
        result.Email.Should().Be(dto.Email);
    }

    /// <summary>
    /// Tests if a `Conflict` exception is thrown when attempting to create a user with an email that already exists.
    /// </summary>
    /// <remarks>
    /// Scenario: An attempt is made to create a user with an email address that is already registered.
    /// Expected: An `Exception` is thrown, indicating a conflict due to the existing email.
    /// Coverage: Email uniqueness validation during user creation.
    /// </remarks>
    [Fact]
    public async Task CreateUserAsync_ShouldThrowConflict_WhenEmailExists()
    {
        var dto = new UserCreateDto
            { Email = "existing@example.com", Password = "Password123!", FullName = "Existing User" };
        _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>()))
            .ReturnsAsync(new User { Email = dto.Email, RoleName = dto.RoleName });

        await Assert.ThrowsAsync<Exception>(() => _userService.CreateUserAsync(dto));
    }

    #endregion

    #region UpdateUserStatusAsync Tests

    /// <summary>
    /// Tests if a user's status can be updated successfully when the user exists.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator updates the status of an existing user (e.g., from Active to Suspended).
    /// Expected: The user's status is updated in the database, and the updated user details are returned.
    /// Coverage: User status update functionality.
    /// </remarks>
    [Fact]
    public async Task UpdateUserStatusAsync_ShouldUpdateStatus_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        var user = new User
            { Id = userId, Email = "test@example.com", Status = UserStatus.Active, RoleName = RoleType.Customer };
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);
        _userRepoMock.Setup(x => x.Update(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _userService.UpdateUserStatusAsync(userId, UserStatus.Suspended, "Vi phạm");

        result.Should().NotBeNull();
        result.Status.Should().Be(UserStatus.Suspended);
    }

    /// <summary>
    /// Tests if a `NotFound` exception is thrown when attempting to update the status of a user that does not exist.
    /// </summary>
    /// <remarks>
    /// Scenario: An attempt is made to update the status of a user ID that does not exist in the system.
    /// Expected: An `Exception` is thrown, indicating that the user was not found.
    /// Coverage: Error handling for updating the status of a non-existent user.
    /// </remarks>
    [Fact]
    public async Task UpdateUserStatusAsync_ShouldThrowNotFound_WhenUserNotExists()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((User)null!);

        await Assert.ThrowsAsync<Exception>(() => _userService.UpdateUserStatusAsync(userId, UserStatus.Active));
    }

    /// <summary>
    /// Tests if a user is reactivated (IsDeleted set to false) when their status is set to Active.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator changes a user's status to `Active` when they were previously suspended or deleted.
    /// Expected: The user's status is set to `Active`, and their `IsDeleted` flag is set to `false`.
    /// Coverage: User reactivation logic.
    /// </remarks>
    [Fact]
    public async Task UpdateUserStatusAsync_ShouldReactivateUser_WhenStatusIsActive()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Status = UserStatus.Suspended,
            IsDeleted = true,
            RoleName = RoleType.Customer,
            FullName = "Test User"
        };
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _userRepoMock.Setup(x => x.Update(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        var result = await _userService.UpdateUserStatusAsync(userId, UserStatus.Active);
        result.Should().NotBeNull();
        result.Status.Should().Be(UserStatus.Active);
        user.IsDeleted.Should().BeFalse();
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