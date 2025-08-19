using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BlindTreasure.UnitTest.Services;

public class AuthServiceTests
{
    private readonly AuthService _authService;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<INotificationService> _notiService;
    private readonly Mock<IGenericRepository<OtpVerification>> _otpVerificationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public AuthServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _emailServiceMock = new Mock<IEmailService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _notiService = new Mock<INotificationService>();
        _configMock = new Mock<IConfiguration>();
        _otpVerificationRepoMock = new Mock<IGenericRepository<OtpVerification>>();

        // JWT config mock
        _configMock.Setup(x => x["JWT:SecretKey"]).Returns("12345678901234567890123456789012");

        // OtpVerifications mock
        _otpVerificationRepoMock
            .Setup(x => x.AddAsync(It.IsAny<OtpVerification>()))
            .ReturnsAsync((OtpVerification o) => o);
        _unitOfWorkMock.Setup(x => x.OtpVerifications).Returns(_otpVerificationRepoMock.Object);

        _authService = new AuthService(
            _loggerServiceMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _emailServiceMock.Object,
            _notiService.Object
        );
    }

    /// <summary>
    /// Tests if a user can be successfully registered when their email does not already exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A new user attempts to register with an email address that is not already in the system.
    /// Expected: The user is successfully registered, added to the database, an OTP verification email is sent, and an OTP verification record is created.
    /// Coverage: User registration, email uniqueness check, OTP generation and email sending.
    /// </remarks>
    [Fact]
    public async Task RegisterUserAsync_ShouldRegisterUser_WhenEmailNotExists()
    {
        // Arrange
        var registrationDto = new UserRegistrationDto
        {
            Email = "test@example.com",
            Password = "Password123!",
            FullName = "Test User",
            DateOfBirth = new DateTime(2000, 1, 1),
            PhoneNumber = "0123456789"
        };

        _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>()))
            .ReturnsAsync((User)null!);
        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((User)null!);
        _unitOfWorkMock.Setup(x => x.Users.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => u);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _emailServiceMock.Setup(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RegisterCustomerAsync(registrationDto);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(registrationDto.Email);
        _unitOfWorkMock.Verify(x => x.Users.AddAsync(It.IsAny<User>()), Times.Once);
        _emailServiceMock.Verify(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
        _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Once);
    }

    /// <summary>
    /// Tests if a user can successfully log in and receive access/refresh tokens with valid credentials.
    /// </summary>
    /// <remarks>
    /// Scenario: A user provides correct email and password for an active, verified account.
    /// Expected: A `LoginResponseDto` containing non-null and non-empty access and refresh tokens is returned, and the user's cache is updated.
    /// Coverage: User authentication, password hashing verification, token generation, and cache updates.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "Password123!"
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = loginDto.Email!,
            Password = new PasswordHasher().HashPassword(loginDto.Password!),
            FullName = "Test User",
            Status = UserStatus.Active,
            RoleName = RoleType.Customer,
            IsEmailVerified = true,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>()))
            .ReturnsAsync((User)null!);
        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.LoginAsync(loginDto, _configMock.Object);

        // Assert
        result.Should().NotBeNull();
        result!.User!.Email.Should().Be(loginDto.Email);
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests if a user's refresh token is cleared and cache is removed upon logout.
    /// </summary>
    /// <remarks>
    /// Scenario: A user requests to log out.
    /// Expected: The user's refresh token in the database is null, and the user's data is removed from the cache.
    /// Coverage: User logout, refresh token invalidation, and cache management.
    /// </remarks>
    [Fact]
    public async Task LogoutAsync_ShouldClearRefreshToken_AndCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Status = UserStatus.Active,
            IsDeleted = false,
            RefreshToken = "refresh-token",
            FullName = "Test User",
            RoleName = RoleType.Customer,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.LogoutAsync(userId);

        // Assert
        result.Should().BeTrue();
        _cacheServiceMock.Verify(x => x.RemoveAsync($"user:{user.Email}"), Times.Once);
    }

    /// <summary>
    /// Tests if new access and refresh tokens are returned when a valid refresh token is provided.
    /// </summary>
    /// <remarks>
    /// Scenario: A user requests to refresh their tokens using a valid and unexpired refresh token.
    /// Expected: New access and refresh tokens are generated and returned.
    /// Coverage: Token refreshing mechanism, refresh token validation.
    /// </remarks>
    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Status = UserStatus.Active,
            IsDeleted = false,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1),
            RoleName = RoleType.Customer,
            FullName = "Test User",
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _authService.RefreshTokenAsync(new TokenRefreshRequestDto { RefreshToken = refreshToken },
            _configMock.Object);

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests if a user's email is verified and their account is activated when a valid OTP is provided.
    /// </summary>
    /// <remarks>
    /// Scenario: A user provides a correct OTP for email verification.
    /// Expected: The user's `IsEmailVerified` status is set to `true`, `Status` is set to `Active`, and a registration success email is sent.
    /// Coverage: OTP verification, user status update, and email confirmation.
    /// </remarks>
    [Fact]
    public async Task VerifyEmailOtpAsync_ShouldActivateUser_WhenOtpIsValid()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "123456";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Status = UserStatus.Pending,
            IsEmailVerified = false,
            FullName = "Test User",
            RoleName = RoleType.Customer,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync(otp);
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        _emailServiceMock.Setup(x => x.SendRegistrationSuccessEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.VerifyEmailOtpAsync(email, otp);

        // Assert
        result.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendRegistrationSuccessEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
    }

    /// <summary>
    /// Tests if a user's password is successfully updated when a valid OTP is provided for password reset.
    /// </summary>
    /// <remarks>
    /// Scenario: A user requests to reset their password and provides a correct OTP.
    /// Expected: The user's password is updated with the new password, and a password change confirmation email is sent.
    /// Coverage: Password reset functionality, OTP validation for password reset, and email notification.
    /// </remarks>
    [Fact]
    public async Task ResetPasswordAsync_ShouldUpdatePassword_WhenOtpIsValid()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "654321";
        var newPassword = "NewPassword123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Status = UserStatus.Active,
            IsEmailVerified = true,
            FullName = "Test User",
            RoleName = RoleType.Customer,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync(otp);
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        _emailServiceMock.Setup(x => x.SendPasswordChangeEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendPasswordChangeEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
    }

    /// <summary>
    /// Tests if a new OTP is sent for registration when the OTP type is 'Register'.
    /// </summary>
    /// <remarks>
    /// Scenario: A user requests to resend an OTP for registration purposes.
    /// Expected: A new OTP is generated and sent via email, and a new OTP verification record is added.
    /// Coverage: OTP resend functionality for registration.
    /// </remarks>
    [Fact]
    public async Task ResendOtpAsync_ShouldCallResendRegisterOtp_WhenTypeIsRegister()
    {
        // Arrange
        var email = "test@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Status = UserStatus.Pending,
            IsEmailVerified = false,
            FullName = "Test User",
            RoleName = RoleType.Customer,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _cacheServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _emailServiceMock.Setup(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.ResendOtpAsync(email, OtpType.Register);

        // Assert
        result.Should().BeTrue();
        _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Once);
    }

    /// <summary>
    /// Tests if a seller can be successfully registered when their email does not already exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A new seller attempts to register with an email address that is not already in the system.
    /// Expected: The seller is successfully registered, both user and seller records are added to the database, an OTP verification email is sent, and an OTP verification record is created.
    /// Coverage: Seller registration, email uniqueness check, OTP generation and email sending for sellers.
    /// </remarks>
    [Fact]
    public async Task RegisterSellerAsync_ShouldRegisterSeller_WhenEmailNotExists()
    {
        // Arrange
        var registrationDto = new SellerRegistrationDto
        {
            Email = "seller@example.com",
            Password = "Password123!",
            FullName = "Test Seller",
            DateOfBirth = new DateTime(2000, 1, 1),
            PhoneNumber = "0987654321",
            CompanyName = "Test Company",
            TaxId = "1234567890",
            CompanyAddress = "123 Test Street"
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((User)null!);
        _unitOfWorkMock.Setup(x => x.Users.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => u);
        _unitOfWorkMock.Setup(x => x.Sellers.AddAsync(It.IsAny<Seller>()))
            .ReturnsAsync((Seller s) => s);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _emailServiceMock.Setup(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RegisterSellerAsync(registrationDto);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(registrationDto.Email);
        result.RoleName.Should().Be(RoleType.Seller);
        _unitOfWorkMock.Verify(x => x.Users.AddAsync(It.IsAny<User>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.Sellers.AddAsync(It.IsAny<Seller>()), Times.Once);
        _emailServiceMock.Verify(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
        _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Once);
    }

    /// <summary>
    /// Tests if an OTP is sent for a forgot password request when the user is valid.
    /// </summary>
    /// <remarks>
    /// Scenario: A valid user requests an OTP to reset their password, and the maximum attempt limit has not been exceeded.
    /// Expected: A new OTP is generated and sent via email, and the OTP attempt count in cache is updated.
    /// Coverage: Forgot password OTP generation and sending, and attempt tracking.
    /// </remarks>
    [Fact]
    public async Task SendForgotPasswordOtpRequestAsync_ShouldSendOtp_WhenUserValid()
    {
        // Arrange
        var email = "test@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            RoleName = RoleType.Customer,
            FullName = "Test User",
            Status = UserStatus.Active,
            IsEmailVerified = true,
            IsDeleted = false
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _cacheServiceMock.Setup(x => x.GetAsync<int?>($"forgot-otp-count:{email}"))
            .ReturnsAsync(0);
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        _otpVerificationRepoMock.Setup(x => x.AddAsync(It.IsAny<OtpVerification>()))
            .ReturnsAsync((OtpVerification o) => o);
        _emailServiceMock.Setup(x => x.SendForgotPasswordOtpEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.ResendOtpAsync(email, OtpType.ForgotPassword);

        // Assert
        result.Should().BeTrue();
        _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Once);
        _emailServiceMock.Verify(x => x.SendForgotPasswordOtpEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
        _cacheServiceMock.Verify(x => x.SetAsync($"forgot-otp-count:{email}", 1, It.IsAny<TimeSpan>()), Times.Once);
    }

    /// <summary>
    /// Tests if a `BadRequest` exception is thrown when a user exceeds the maximum attempts for sending a forgot password OTP.
    /// </summary>
    /// <remarks>
    /// Scenario: A user requests to resend a forgot password OTP after already exceeding the maximum allowed attempts.
    /// Expected: An `Exception` with a 400 (Bad Request) status code is thrown, and no new OTP is generated or email sent.
    /// Coverage: Rate limiting for OTP requests and error handling for exceeding attempts.
    /// </remarks>
    [Fact]
    public async Task SendForgotPasswordOtpRequestAsync_ShouldThrowBadRequest_WhenExceedMaxAttempts()
    {
        // Arrange
        var email = "test@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            RoleName = RoleType.Customer,
            FullName = "Test User",
            Status = UserStatus.Active,
            IsEmailVerified = true,
            IsDeleted = false
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _cacheServiceMock.Setup(x => x.GetAsync<int?>($"forgot-otp-count:{email}"))
            .ReturnsAsync(3); // Đã gửi 3 lần, vượt quá giới hạn

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _authService.ResendOtpAsync(email, OtpType.ForgotPassword));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(400);
        _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Never);
        _emailServiceMock.Verify(x => x.SendForgotPasswordOtpEmailAsync(It.IsAny<EmailRequestDto>()), Times.Never);
    }

    /// <summary>
    /// Tests if a welcome notification is sent to a user upon their first successful login.
    /// </summary>
    /// <remarks>
    /// Scenario: A user logs in for the first time.
    /// Expected: A welcome notification is pushed to the user, and a flag is set in cache to prevent sending duplicate welcome notifications.
    /// Coverage: First-time login experience, notification system integration, and cache management for notification state.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_ShouldSendWelcomeNotification_OnFirstLogin()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "Password123!"
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = loginDto.Email!,
            Password = new PasswordHasher().HashPassword(loginDto.Password!),
            FullName = "Test User",
            Status = UserStatus.Active,
            RoleName = RoleType.Customer,
            IsEmailVerified = true,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>()))
            .ReturnsAsync((User)null!);
        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.ExistsAsync($"noti:welcome:{user.Id}"))
            .ReturnsAsync(false); // Chưa gửi thông báo welcome
        _notiService.Setup(x => x.PushNotificationToUser(
                It.IsAny<Guid>(), It.IsAny<NotificationDto>()))
            .ReturnsAsync(new Notification
            {
                Type = NotificationType.System,
                Title = "Chào mừng!",
                Message = "Chào mừng quay trở lại BlindTreasure.",
                Id = Guid.NewGuid(),
                UserId = user.Id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

        // Act
        var result = await _authService.LoginAsync(loginDto, _configMock.Object);

        // Assert
        result.Should().NotBeNull();
        _notiService.Verify(x => x.PushNotificationToUser(
                user.Id, It.Is<NotificationDto>(n =>
                    n.Title == "Chào mừng!" &&
                    n.Type == NotificationType.System)),
            Times.Once);
        _cacheServiceMock.Verify(x => x.SetAsync(
                $"noti:welcome:{user.Id}", true, It.IsAny<TimeSpan>()),
            Times.Once);
    }

    /// <summary>
    /// Tests if seller-specific information is included in the login response when the logged-in user is a seller.
    /// </summary>
    /// <remarks>
    /// Scenario: A user with the `Seller` role logs in.
    /// Expected: The `LoginResponseDto` contains the `SellerId` and `RoleName` is correctly identified as `Seller`.
    /// Coverage: Role-based login handling and inclusion of associated entity information.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_ShouldIncludeSellerInfo_WhenUserIsSeller()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "seller@example.com",
            Password = "Password123!"
        };
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = loginDto.Email!,
            Password = new PasswordHasher().HashPassword(loginDto.Password!),
            FullName = "Test Seller",
            Status = UserStatus.Active,
            RoleName = RoleType.Seller,
            IsEmailVerified = true,
            DateOfBirth = new DateTime(2000, 1, 1)
        };
        var seller = new Seller
        {
            Id = sellerId,
            UserId = userId,
            CompanyName = "Test Company",
            TaxId = "1234567890",
            Status = SellerStatus.Approved,
            IsVerified = true
        };

        _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>()))
            .ReturnsAsync((User)null!);
        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Sellers.FirstOrDefaultAsync(It.IsAny<Expression<Func<Seller, bool>>>()))
            .ReturnsAsync(seller);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _authService.LoginAsync(loginDto, _configMock.Object);

        // Assert
        result.Should().NotBeNull();
        result!.User.Should().NotBeNull();
        result.User!.SellerId.Should().Be(sellerId);
        result.User.RoleName.Should().Be(RoleType.Seller);
    }

    /// <summary>
    /// Tests if a seller verification email is sent when a seller user's email is verified with OTP.
    /// </summary>
    /// <remarks>
    /// Scenario: A seller user provides a correct OTP to verify their email.
    /// Expected: A seller-specific email verification success email is sent, and the user's status and email verification status are updated.
    /// Coverage: Seller-specific email verification flow and differentiation from customer verification.
    /// </remarks>
    [Fact]
    public async Task VerifyEmailOtpAsync_ShouldSendSellerVerificationEmail_WhenUserIsSeller()
    {
        // Arrange
        var email = "seller@example.com";
        var otp = "123456";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Status = UserStatus.Pending,
            IsEmailVerified = false,
            FullName = "Test Seller",
            RoleName = RoleType.Seller,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        _cacheServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync(otp);
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        _emailServiceMock.Setup(x => x.SendSellerEmailVerificationSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.VerifyEmailOtpAsync(email, otp);

        // Assert
        result.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendSellerEmailVerificationSuccessAsync(It.IsAny<EmailRequestDto>()),
            Times.Once);
        _emailServiceMock.Verify(x => x.SendRegistrationSuccessEmailAsync(It.IsAny<EmailRequestDto>()), Times.Never);
        user.Status.Should().Be(UserStatus.Active);
        user.IsEmailVerified.Should().BeTrue();
    }

    /// <summary>
    /// Tests if a `Conflict` exception is thrown when a customer attempts to register with an email that already exists.
    /// </summary>
    /// <remarks>
    /// Scenario: A new customer attempts to register with an email address that is already associated with an existing user.
    /// Expected: An `Exception` with a 409 (Conflict) status code is thrown, and no new user or OTP record is created.
    /// Coverage: Email uniqueness validation during customer registration and error handling for conflicts.
    /// </remarks>
    [Fact]
    public async Task RegisterCustomerAsync_ShouldThrowConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        var registrationDto = new UserRegistrationDto
        {
            Email = "existing@example.com",
            Password = "Password123!",
            FullName = "Existing User",
            DateOfBirth = new DateTime(2000, 1, 1),
            PhoneNumber = "0123456789"
        };

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = registrationDto.Email,
            FullName = "Existing User",
            RoleName = RoleType.Customer,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };

        _cacheServiceMock.Setup(x => x.GetAsync<User>($"user:{registrationDto.Email}"))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _authService.RegisterCustomerAsync(registrationDto));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(409);
        _unitOfWorkMock.Verify(x => x.Users.AddAsync(It.IsAny<User>()), Times.Never);
        _emailServiceMock.Verify(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()), Times.Never);
        _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Never);
    }

    /// <summary>
    /// Tests if a `Forbidden` exception is thrown when an inactive user attempts to log in.
    /// </summary>
    /// <remarks>
    /// Scenario: A user with `Pending` or other inactive status attempts to log in.
    /// Expected: An `Exception` with a 403 (Forbidden) status code is thrown, and no user data or notifications are updated.
    /// Coverage: User status validation during login and access control.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_ShouldThrowForbidden_WhenUserNotActive()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "pending@example.com",
            Password = "Password123!"
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = loginDto.Email!,
            Password = new PasswordHasher().HashPassword(loginDto.Password!),
            FullName = "Pending User",
            Status = UserStatus.Pending, // Người dùng chưa kích hoạt
            RoleName = RoleType.Customer,
            IsEmailVerified = false,
            DateOfBirth = new DateTime(2000, 1, 1)
        };

        _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>()))
            .ReturnsAsync((User)null!);
        _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<Exception>(() => _authService.LoginAsync(loginDto, _configMock.Object));

        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(403);
        _unitOfWorkMock.Verify(x => x.Users.Update(It.IsAny<User>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
        _notiService.Verify(x => x.PushNotificationToUser(
                It.IsAny<Guid>(), It.IsAny<NotificationDto>()),
            Times.Never);
    }
}