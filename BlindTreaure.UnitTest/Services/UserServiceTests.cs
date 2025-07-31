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

namespace BlindTreaure.UnitTest.Services;

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

    #region GetUserDetailsByIdAsync Tests

    [Fact]
    public async Task GetUserDetailsByIdAsync_ShouldReturnUserDto_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            RoleName = RoleType.Customer,
            IsDeleted = false,
            Email = "hehe@gmail.com",
            FullName = "Test User"
        };
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        var result = await _userService.GetUserDetailsByIdAsync(userId);
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserDetailsByIdAsync_ShouldThrowNotFound_WhenUserIsDeleted()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, IsDeleted = true, Email = "hehe@gmail.com", RoleName = RoleType.Customer };
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);

        await Assert.ThrowsAsync<Exception>(() => _userService.GetUserDetailsByIdAsync(userId));
    }

    #endregion

    #region UpdateProfileAsync Tests

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

    [Fact]
    public async Task UpdateProfileAsync_ShouldThrowNotFound_WhenUserNotExists()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User)null!);

        await Assert.ThrowsAsync<Exception>(() => _userService.UpdateProfileAsync(userId, new UpdateProfileDto()));
    }

    #endregion

    #region UploadAvatarAsync Tests

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

    #region GetAllUsersAsync Tests

    [Fact]
    public async Task GetAllUsersAsync_ShouldReturnPaginatedUsers_WhenDataExists()
    {
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(), FullName = "User 1", IsDeleted = false, Email = "hehe@gmail.com",
                RoleName = RoleType.Customer
            },
            new()
            {
                Id = Guid.NewGuid(), FullName = "User 2", IsDeleted = false, Email = "hehe@gmail.com",
                RoleName = RoleType.Customer
            }
        };
        var mockQueryable = users.AsQueryable().BuildMock();
        _userRepoMock.Setup(x => x.GetQueryable()).Returns(mockQueryable);

        var param = new UserQueryParameter { PageIndex = 1, PageSize = 10 };

        var result = await _userService.GetAllUsersAsync(param);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
    }

    #endregion

    #region CreateUserAsync Tests

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

    [Fact]
    public async Task UpdateUserStatusAsync_ShouldThrowNotFound_WhenUserNotExists()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((User)null!);

        await Assert.ThrowsAsync<Exception>(() => _userService.UpdateUserStatusAsync(userId, UserStatus.Active));
    }

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

    #region GetUserByEmail Tests

    [Fact]
    public async Task GetUserByEmail_ShouldReturnUser_WhenExists()
    {
        var email = "test@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            IsDeleted = false,
            RoleName = RoleType.Customer,
            FullName = "Test User"
        };
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(user);
        var result = await _userService.GetUserByEmail(email);
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
    }

    #endregion

    #region GetUserById Tests

    [Fact]
    public async Task GetUserById_ShouldReturnUser_WhenExists()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            RoleName = RoleType.Customer,
            FullName = "Test User",
            IsDeleted = false
        };
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        var result = await _userService.GetUserById(userId);
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserById_ShouldReturnNull_WhenNotExists()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((User)null!);
        var result = await _userService.GetUserById(userId);
        result.Should().BeNull();
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