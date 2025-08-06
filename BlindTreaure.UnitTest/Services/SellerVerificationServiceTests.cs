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

    /// <summary>
    /// Checks if a seller's verification request is successfully approved when instructed to do so.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator reviews a seller's profile and approves their verification request.
    /// Expected: The seller's account is marked as verified and approved, and they receive a notification and an email confirming the approval. The system's temporary data (cache) for this seller is also cleared.
    /// Coverage: The full process of approving a seller, including updating their status, sending communications, and managing cached data.
    /// </remarks>
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
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
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
                It.Is<NotificationDto>(n =>
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

    /// <summary>
    /// Checks if a seller's verification request is correctly rejected when instructed to do so.
    /// </summary>
    /// <remarks>
    /// Scenario: An administrator reviews a seller's profile and decides to reject their verification request, providing a reason.
    /// Expected: The seller's account is marked as rejected, and they receive a notification and an email explaining the rejection and the reason. The system's temporary data (cache) for this seller is also cleared.
    /// Coverage: The full process of rejecting a seller, including updating their status, communicating the rejection, and managing cached data.
    /// </remarks>
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
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
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
                It.Is<NotificationDto>(n =>
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

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to verify a seller that doesn't exist.
    /// </summary>
    /// <remarks>
    /// Scenario: An attempt is made to approve or reject a seller's profile using an ID that doesn't belong to any existing seller.
    /// Expected: The system responds with a 'Not Found' error, indicating that the seller's profile could not be located.
    /// Coverage: Error handling when trying to verify a non-existent seller, ensuring robust system responses.
    /// </remarks>
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

    /// <summary>
    /// Checks if an internal error occurs when a seller's profile is found but the associated user information is missing.
    /// </summary>
    /// <remarks>
    /// Scenario: The system finds a seller's profile for verification, but the user account linked to it is unexpectedly missing or null.
    /// Expected: An internal error is thrown, indicating a data inconsistency or critical system issue, as every seller should have a linked user account.
    /// Coverage: Handling critical data integrity issues where seller profiles are not properly linked to user accounts.
    /// </remarks>
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

    /// <summary>
    /// Checks if the correct email is sent with accurate information when a seller's profile is approved.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller's verification request is approved, and the system needs to send them an approval email.
    /// Expected: An email is sent to the seller's registered email address, containing their name and a confirmation of approval.
    /// Coverage: The accuracy and content of automated emails sent upon seller approval.
    /// </remarks>
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
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
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

    /// <summary>
    /// Checks if the correct email is sent with accurate information and the rejection reason when a seller's profile is rejected.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller's verification request is rejected, and the system needs to send them a rejection email with the specific reason.
    /// Expected: An email is sent to the seller's registered email address, containing their name and the detailed reason for rejection.
    /// Coverage: The accuracy and content of automated emails sent upon seller rejection, including the communication of the rejection reason.
    /// </remarks>
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
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
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

    /// <summary>
    /// Checks if the relevant cache entries are cleared when a seller's profile is approved.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller's verification request is approved, and their old cached information needs to be removed to ensure fresh data is loaded next time.
    /// Expected: The cache entries specifically related to the seller's ID and user ID are removed from the system's temporary storage.
    /// Coverage: Cache invalidation upon seller approval to prevent serving outdated seller information.
    /// </remarks>
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
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
            .ReturnsAsync(mockNotification);

        _emailServiceMock
            .Setup(x => x.SendSellerApprovalSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Setup cache mocks to capture the cache keys
        var removedCacheKeys = new List<string>();
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

    /// <summary>
    /// Checks if the seller's status and verification flag are correctly updated upon approval.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller's verification request is approved.
    /// Expected: The seller's status is set to 'Approved', their `IsVerified` flag is set to `true`, and any previous rejection reason is cleared.
    /// Coverage: Proper status and flag updates in the database when a seller is approved.
    /// </remarks>
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
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
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

    /// <summary>
    /// Checks if the notification sent to a seller upon approval has the correct content.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller's verification request is approved, triggering an automated notification.
    /// Expected: A system notification is pushed to the seller with a title confirming approval and a message indicating successful verification.
    /// Coverage: The accuracy and relevance of the notification content for approved sellers.
    /// </remarks>
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

        NotificationDto capturedNotification = null;
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
            .Callback<Guid, NotificationDto>((id, notification) => capturedNotification = notification)
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

    /// <summary>
    /// Checks if the notification sent to a seller upon rejection has the correct content, including the reason.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller's verification request is rejected, triggering an automated notification with the rejection reason.
    /// Expected: A system notification is pushed to the seller with a title indicating rejection and a message that clearly states the reason for denial.
    /// Coverage: The accuracy and completeness of the notification content for rejected sellers, ensuring the reason for rejection is communicated.
    /// </remarks>
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

        NotificationDto capturedNotification = null;
        _notificationServiceMock
            .Setup(x => x.PushNotificationToUser(It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
            .Callback<Guid, NotificationDto>((id, notification) => capturedNotification = notification)
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