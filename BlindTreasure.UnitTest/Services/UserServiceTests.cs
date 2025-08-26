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
    private readonly AdminService _userService;

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
    /// Tests successful user profile update with valid input data
    /// </summary>
    /// <remarks>
    /// Scenario: User provides valid profile information including full name and phone number for profile update
    /// Expected: Profile is successfully updated and returns updated UserDto object with new information
    /// Coverage: Normal case testing for profile update functionality with valid data validation
    /// TestType: Normal
    /// InputConditions: Valid user ID exists in system, Valid UpdateProfileDto with FullName and PhoneNumber provided, User is not deleted, User has Customer role
    /// ExpectedResult: Updated UserDto object with new profile information
    /// ExpectedReturnValue: UserDto object containing updated profile data
    /// ExceptionExpected: false
    /// LogMessage: Profile updated successfully
    /// </remarks>
    [Fact]
    public async Task UpdateProfileAsync_Should_UpdateProfile_When_ValidData()
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
    /// Tests exception handling when updating profile for non-existent user
    /// </summary>
    /// <remarks>
    /// Scenario: Attempt to update profile using a user ID that does not exist in the database
    /// Expected: Exception is thrown indicating user not found
    /// Coverage: Abnormal case testing for profile update with invalid user ID
    /// TestType: Abnormal  
    /// InputConditions: User ID does not exist in database, Valid UpdateProfileDto provided
    /// ExpectedResult: Exception thrown
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: User not found
    /// </remarks>
    [Fact]
    public async Task UpdateProfileAsync_Should_ThrowException_When_UserNotExists()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User)null!);

        await Assert.ThrowsAsync<Exception>(() => _userService.UpdateProfileAsync(userId, new UpdateProfileDto()));
    }

    #endregion

    #region UploadAvatarAsync Tests

    /// <summary>
    /// Tests successful avatar upload with valid image file
    /// </summary>
    /// <remarks>
    /// Scenario: User uploads a valid image file as their profile avatar
    /// Expected: Avatar is successfully uploaded to blob storage and user record is updated with new avatar URL
    /// Coverage: Normal case testing for avatar upload functionality with blob storage integration
    /// TestType: Normal
    /// InputConditions: Valid user ID exists in system, Valid IFormFile with image content provided, File size is greater than 0, User is not deleted
    /// ExpectedResult: UserDto object with updated avatar URL
    /// ExpectedReturnValue: UserDto object containing avatar URL
    /// ExceptionExpected: false
    /// LogMessage: Avatar uploaded successfully
    /// </remarks>
    [Fact]
    public async Task UploadAvatarAsync_Should_UploadAvatar_When_ValidFile()
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
    /// Tests exception handling when uploading empty avatar file
    /// </summary>
    /// <remarks>
    /// Scenario: User attempts to upload an empty file or file with zero bytes as avatar
    /// Expected: Exception is thrown due to invalid file input
    /// Coverage: Boundary case testing for avatar upload with empty file validation
    /// TestType: Boundary
    /// InputConditions: Valid user ID exists in system, IFormFile with zero length provided, File is empty
    /// ExpectedResult: Exception thrown
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: Invalid input data
    /// </remarks>
    [Fact]
    public async Task UploadAvatarAsync_Should_ThrowException_When_FileIsEmpty()
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
    /// Tests successful user creation with unique email address
    /// </summary>
    /// <remarks>
    /// Scenario: New user registration with email address that does not exist in the system
    /// Expected: User is successfully created in database and returns new UserDto object
    /// Coverage: Normal case testing for user creation functionality with email uniqueness validation
    /// TestType: Normal
    /// InputConditions: Email does not exist in cache, Email does not exist in database, Valid UserCreateDto with all required fields, Password meets requirements
    /// ExpectedResult: UserDto object with new user information
    /// ExpectedReturnValue: UserDto object
    /// ExceptionExpected: false
    /// LogMessage: User created successfully
    /// </remarks>
    [Fact]
    public async Task CreateUserAsync_Should_CreateUser_When_EmailNotExists()
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
    /// Tests exception handling when creating user with existing email
    /// </summary>
    /// <remarks>
    /// Scenario: Attempt to create new user with email address that already exists in the system
    /// Expected: Exception is thrown indicating email conflict
    /// Coverage: Abnormal case testing for user creation with duplicate email validation
    /// TestType: Abnormal
    /// InputConditions: Email already exists in cache, Valid UserCreateDto provided
    /// ExpectedResult: Exception thrown
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: Email already exists
    /// </remarks>
    [Fact]
    public async Task CreateUserAsync_Should_ThrowException_When_EmailExists()
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
    /// Tests successful user status update for existing user
    /// </summary>
    /// <remarks>
    /// Scenario: Administrator updates the status of an existing user from Active to Suspended
    /// Expected: User status is successfully updated in database and returns updated UserDto object
    /// Coverage: Normal case testing for user status update functionality
    /// TestType: Normal
    /// InputConditions: Valid user ID exists in system, Valid UserStatus provided, User exists in database, Valid suspension reason provided
    /// ExpectedResult: UserDto object with updated status
    /// ExpectedReturnValue: Updated UserDto
    /// ExceptionExpected: false
    /// LogMessage: User status updated successfully
    /// </remarks>
    [Fact]
    public async Task UpdateUserStatusAsync_Should_UpdateStatus_When_UserExists()
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
    /// Tests exception handling when updating status for non-existent user
    /// </summary>
    /// <remarks>
    /// Scenario: Attempt to update status using a user ID that does not exist in the database
    /// Expected: Exception is thrown indicating user not found
    /// Coverage: Abnormal case testing for status update with invalid user ID
    /// TestType: Abnormal
    /// InputConditions: User ID does not exist in database, Valid UserStatus provided
    /// ExpectedResult: Exception thrown
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: User not found
    /// </remarks>
    [Fact]
    public async Task UpdateUserStatusAsync_Should_ThrowException_When_UserNotExists()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((User)null!);

        await Assert.ThrowsAsync<Exception>(() => _userService.UpdateUserStatusAsync(userId, UserStatus.Active));
    }

    /// <summary>
    /// Tests user reactivation when status is set to Active
    /// </summary>
    /// <remarks>
    /// Scenario: Administrator changes user status to Active for a previously suspended and deleted user
    /// Expected: User status is set to Active and IsDeleted flag is set to false for reactivation
    /// Coverage: Boundary case testing for user reactivation logic when status becomes Active
    /// TestType: Boundary
    /// InputConditions: User exists in database, User status is currently Suspended, User IsDeleted flag is true, Status is set to Active
    /// ExpectedResult: UserDto object with Active status and user reactivated
    /// ExpectedReturnValue: Reactivated UserDto
    /// ExceptionExpected: false
    /// LogMessage: User reactivated successfully
    /// </remarks>
    [Fact]
    public async Task UpdateUserStatusAsync_Should_ReactivateUser_When_StatusIsActive()
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