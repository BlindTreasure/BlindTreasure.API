using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Moq;
using System.Linq.Expressions;

namespace BlindTreaure.UnitTest.Services;

public class SellerVerificationServiceTests
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<Seller>> _sellerRepoMock;
    private readonly SellerVerificationService _sellerVerificationService;

    public SellerVerificationServiceTests()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _emailServiceMock = new Mock<IEmailService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sellerRepoMock = new Mock<IGenericRepository<Seller>>();

        _unitOfWorkMock.Setup(x => x.Sellers).Returns(_sellerRepoMock.Object);

        _sellerVerificationService = new SellerVerificationService(
            _unitOfWorkMock.Object,
            _emailServiceMock.Object,
            _cacheServiceMock.Object,
            _notificationServiceMock.Object
        );
    }

    #region VerifySellerAsync Tests

    [Fact]
    public async Task VerifySellerAsync_ShouldApproveSeller_WhenIsApprovedIsTrue()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "seller@example.com",
                FullName = "Test Seller",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = true
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Setup notification mock with correct return type
        var mockNotification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Đã duyệt hồ sơ",
            Type = NotificationType.System,
            Message = "m đã bi gay"
        };
        
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .ReturnsAsync(mockNotification);

        // Setup email mock
        _emailServiceMock
            .Setup(x => x.SendSellerApprovalSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        result.Should().BeTrue();
        seller.IsVerified.Should().BeTrue();
        seller.Status.Should().Be(SellerStatus.Approved);
        seller.RejectReason.Should().BeNull();

        // Verify notifications were sent
        _notificationServiceMock.Verify(
            x => x.PushNotificationToUser(
                userId,
                It.Is<NotificationDTO>(n =>
                    n.Type == NotificationType.System &&
                    n.Title == "Đã duyệt hồ sơ")),
            Times.Once);

        // Verify email was sent
        _emailServiceMock.Verify(
            x => x.SendSellerApprovalSuccessAsync(
                It.Is<EmailRequestDto>(e =>
                    e.To == "seller@example.com" &&
                    e.UserName == "Test Seller")),
            Times.Once);

        // Verify cache was cleared
        _cacheServiceMock.Verify(
            x => x.RemoveAsync($"seller:{sellerId}"),
            Times.Once);
        _cacheServiceMock.Verify(
            x => x.RemoveAsync($"seller:user:{userId}"),
            Times.Once);
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldRejectSeller_WhenIsApprovedIsFalse()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rejectReason = "Documents are not valid";

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "rejected@example.com",
                FullName = "Rejected Seller",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = false,
            RejectReason = rejectReason
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Setup notification mock with correct return type
        var mockNotification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Hồ sơ bị từ chối",
            Message = $"Hồ sơ seller của bạn đã bị từ chối. Lý do: {rejectReason}",
            Type = NotificationType.System
        };
        
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .ReturnsAsync(mockNotification);

        // Setup email mock
        _emailServiceMock
            .Setup(x => x.SendSellerRejectionAsync(It.IsAny<EmailRequestDto>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        result.Should().BeTrue();
        seller.IsVerified.Should().BeFalse();
        seller.Status.Should().Be(SellerStatus.Rejected);
        seller.RejectReason.Should().Be(rejectReason);

        // Verify notifications were sent
        _notificationServiceMock.Verify(
            x => x.PushNotificationToUser(
                userId,
                It.Is<NotificationDTO>(n =>
                    n.Type == NotificationType.System &&
                    n.Title == "Hồ sơ bị từ chối" &&
                    n.Message.Contains(rejectReason))),
            Times.Once);

        // Verify email was sent
        _emailServiceMock.Verify(
            x => x.SendSellerRejectionAsync(
                It.Is<EmailRequestDto>(e =>
                    e.To == "rejected@example.com" &&
                    e.UserName == "Rejected Seller"),
                rejectReason),
            Times.Once);

        // Verify cache was cleared
        _cacheServiceMock.Verify(
            x => x.RemoveAsync($"seller:{sellerId}"),
            Times.Once);
        _cacheServiceMock.Verify(
            x => x.RemoveAsync($"seller:user:{userId}"),
            Times.Once);
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldThrowNotFound_WhenSellerNotExists()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var verificationDto = new SellerVerificationDto
        {
            IsApproved = true
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync((Seller)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto));

        exception.Message.Should().Contain("Không tìm thấy hồ sơ seller");
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldThrowInternal_WhenUserIsNull()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = null // User is null
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = true
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto));

        exception.Message.Should().Contain("Không tìm thấy thông tin người dùng");
    }

    #endregion

    #region Additional Test Cases

    [Fact]
    public async Task VerifySellerAsync_ShouldSendEmailWithCorrectData_WhenApproved()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "seller@example.com",
                FullName = "Email Test Seller",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = true
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        var mockNotification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Đã duyệt hồ sơ",
            Type = NotificationType.System,
            Message = "Hồ sơ đã được duyệt"
        };
        
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .ReturnsAsync(mockNotification);

        EmailRequestDto capturedEmailRequest = null;
        _emailServiceMock
            .Setup(x => x.SendSellerApprovalSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Callback<EmailRequestDto>(request => capturedEmailRequest = request)
            .Returns(Task.CompletedTask);

        // Act
        await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        capturedEmailRequest.Should().NotBeNull();
        capturedEmailRequest.To.Should().Be("seller@example.com");
        capturedEmailRequest.UserName.Should().Be("Email Test Seller");
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldSendEmailWithCorrectData_WhenRejected()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rejectReason = "Missing required documents";

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "rejected.email@example.com",
                FullName = "Email Rejection Test",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = false,
            RejectReason = rejectReason
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        var mockNotification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Hồ sơ bị từ chối",
            Type = NotificationType.System,
            Message = $"Hồ sơ seller của bạn đã bị từ chối. Lý do: {rejectReason}"
        };
        
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .ReturnsAsync(mockNotification);

        EmailRequestDto capturedEmailRequest = null;
        string capturedRejectReason = null;
        _emailServiceMock
            .Setup(x => x.SendSellerRejectionAsync(It.IsAny<EmailRequestDto>(), It.IsAny<string>()))
            .Callback<EmailRequestDto, string>((request, reason) => 
            {
                capturedEmailRequest = request;
                capturedRejectReason = reason;
            })
            .Returns(Task.CompletedTask);

        // Act
        await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        capturedEmailRequest.Should().NotBeNull();
        capturedEmailRequest.To.Should().Be("rejected.email@example.com");
        capturedEmailRequest.UserName.Should().Be("Email Rejection Test");
        capturedRejectReason.Should().Be(rejectReason);
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldClearCache_WhenApproved()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "cache.test@example.com",
                FullName = "Cache Test Seller",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = true
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        var mockNotification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Đã duyệt hồ sơ",
            Type = NotificationType.System,
            Message = "Hồ sơ đã được duyệt"
        };
        
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .ReturnsAsync(mockNotification);

        _emailServiceMock
            .Setup(x => x.SendSellerApprovalSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Setup cache mocks to capture the cache keys
        List<string> removedCacheKeys = new List<string>();
        _cacheServiceMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Callback<string>(key => removedCacheKeys.Add(key))
            .Returns(Task.CompletedTask);

        // Act
        await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        removedCacheKeys.Should().Contain($"seller:{sellerId}");
        removedCacheKeys.Should().Contain($"seller:user:{userId}");
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldSetCorrectStatus_WhenApproved()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "status.test@example.com",
                FullName = "Status Test Seller",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = true
        };

        Seller capturedSellerUpdate = null;
        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .Callback<Seller>(s => capturedSellerUpdate = s)
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        var mockNotification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Đã duyệt hồ sơ",
            Type = NotificationType.System,
            Message = "Hồ sơ đã được duyệt"
        };
        
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .ReturnsAsync(mockNotification);

        _emailServiceMock
            .Setup(x => x.SendSellerApprovalSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        capturedSellerUpdate.Should().NotBeNull();
        capturedSellerUpdate.Status.Should().Be(SellerStatus.Approved);
        capturedSellerUpdate.IsVerified.Should().BeTrue();
        capturedSellerUpdate.RejectReason.Should().BeNull();
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldSetCorrectNotificationContent_WhenApproved()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "notification.test@example.com",
                FullName = "Notification Test Seller",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = true
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        NotificationDTO capturedNotification = null;
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .Callback<Guid, NotificationDTO>((id, notification) => capturedNotification = notification)
            .ReturnsAsync(new Notification
            {
                Id = Guid.NewGuid(),
                Type = NotificationType.System,
                Title = "Đã duyệt hồ sơ",
                Message = "Hồ sơ đã được duyệt thành công",
                UserId = userId
            });

        _emailServiceMock
            .Setup(x => x.SendSellerApprovalSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification.Title.Should().Be("Đã duyệt hồ sơ");
        capturedNotification.Type.Should().Be(NotificationType.System);
        capturedNotification.Message.Should().Contain("đã được duyệt thành công");
    }

    [Fact]
    public async Task VerifySellerAsync_ShouldSetCorrectNotificationContent_WhenRejected()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rejectReason = "Invalid business registration";

        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            Status = SellerStatus.WaitingReview,
            IsVerified = false,
            User = new User
            {
                Id = userId,
                Email = "notification.reject@example.com",
                FullName = "Notification Reject Test",
                RoleName = RoleType.Seller
            }
        };

        var verificationDto = new SellerVerificationDto
        {
            IsApproved = false,
            RejectReason = rejectReason
        };

        _sellerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Seller, bool>>>(),
                It.IsAny<Expression<Func<Seller, object>>>()))
            .ReturnsAsync(seller);

        _sellerRepoMock.Setup(x => x.Update(It.IsAny<Seller>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        NotificationDTO capturedNotification = null;
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDTO>()))
            .Callback<Guid, NotificationDTO>((id, notification) => capturedNotification = notification)
            .ReturnsAsync(new Notification
            {
                Id = Guid.NewGuid(),
                Type = NotificationType.System,
                Title = "Hồ sơ bị từ chối",
                Message = $"Hồ sơ seller của bạn đã bị từ chối. Lý do: {rejectReason}",
                UserId = userId
            });

        _emailServiceMock
            .Setup(x => x.SendSellerRejectionAsync(It.IsAny<EmailRequestDto>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sellerVerificationService.VerifySellerAsync(sellerId, verificationDto);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification.Title.Should().Be("Hồ sơ bị từ chối");
        capturedNotification.Type.Should().Be(NotificationType.System);
        capturedNotification.Message.Should().Contain(rejectReason);
    }

    #endregion
}