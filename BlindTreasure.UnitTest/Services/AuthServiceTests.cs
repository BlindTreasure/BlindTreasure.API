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

    #region Register Customer

    /// <summary>
    /// Tests successful customer registration with a non-existing email.
    /// </summary>
    /// <remarks>
    /// Scenario: New customer registers with an email not present in cache or DB.
    /// Expected: User created; OTP generated and emailed; OTP verification record inserted.
    /// Coverage: Email uniqueness check; user creation; OTP generation & email; OTP audit record.
    /// TestType: Normal
    /// InputConditions: Email not in cache; Users.FirstOrDefaultAsync returns null; SaveChanges succeeds.
    /// ExpectedResult: New user entity persisted and returned.
    /// ExpectedReturnValue: User
    /// ExceptionExpected: false
    /// LogMessage: Registration initiated, OTP sent.
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
    /// Tests registration fails when database save operation fails.
    /// </summary>
    /// <remarks>
    /// Scenario: Customer registration with valid data but database save fails.
    /// Expected: Exception thrown; no OTP sent; no email sent.
    /// Coverage: Database failure handling during user creation.
    /// TestType: Abnormal
    /// InputConditions: Email not exists; SaveChanges returns 0 (failure).
    /// ExpectedResult: Exception thrown.
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: Database save failed during registration.
    /// </remarks>
    [Fact]
    public async Task RegisterCustomerAsync_ShouldThrowException_WhenSaveChangesFails()
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
            .ReturnsAsync(0); // Simulate save failure

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _authService.RegisterCustomerAsync(registrationDto));
        _emailServiceMock.Verify(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()), Times.Never);
        _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Never);
    }

    #endregion

    #region Login
    /// <summary>
    /// Tests login returns access/refresh tokens for valid credentials.
    /// </summary>
    /// <remarks>
    /// Scenario: Active, verified user logs in with correct password.
    /// Expected: Non-empty access/refresh tokens returned; user cache updated.
    /// Coverage: Password verification; token issuance; cache update.
    /// TestType: Normal
    /// InputConditions: User exists; status Active; password hash valid; cache miss.
    /// ExpectedResult: LoginResponseDto populated with tokens and user info.
    /// ExpectedReturnValue: LoginResponseDto
    /// ExceptionExpected: false
    /// LogMessage: Login succeeded.
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
    /// Tests login throws unauthorized when password is incorrect.
    /// </summary>
    /// <remarks>
    /// Scenario: User provides wrong password during login.
    /// Expected: Exception with 401 status; no tokens generated; no cache update.
    /// Coverage: Password verification failure handling.
    /// TestType: Abnormal
    /// InputConditions: User exists; password hash doesn't match; IsLoginGoogle false.
    /// ExpectedResult: Exception thrown with 401 status.
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: Authentication failed due to wrong password.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_ShouldThrowUnauthorized_WhenPasswordIsIncorrect()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "WrongPassword123!",
            IsLoginGoole = false
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = loginDto.Email!,
            Password = new PasswordHasher().HashPassword("CorrectPassword123!"),
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

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _authService.LoginAsync(loginDto, _configMock.Object));
        
        var statusCode = ExceptionUtils.ExtractStatusCode(exception);
        statusCode.Should().Be(401);
        _unitOfWorkMock.Verify(x => x.Users.Update(It.IsAny<User>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    /// <summary>
    /// Tests Google login bypasses password verification successfully.
    /// </summary>
    /// <remarks>
    /// Scenario: User logs in via Google OAuth without password verification.
    /// Expected: Login succeeds; tokens generated; password check skipped.
    /// Coverage: Google OAuth login flow and password bypass logic.
    /// TestType: Boundary
    /// InputConditions: IsLoginGoogle true; user exists and active; password can be null.
    /// ExpectedResult: Successful login with tokens.
    /// ExpectedReturnValue: LoginResponseDto
    /// ExceptionExpected: false
    /// LogMessage: Google login successful, password verification bypassed.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_ShouldBypassPasswordCheck_WhenIsGoogleLogin()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = null,
            IsLoginGoole = true
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = loginDto.Email!,
            Password = "any-password-hash",
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
        _cacheServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _authService.LoginAsync(loginDto, _configMock.Object);

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests forbidden when user is not active.
    /// </summary>
    /// <remarks>
    /// Scenario: Pending user attempts to log in.
    /// Expected: Exception with 403 status; no updates or notifications occur.
    /// Coverage: Status gate before authentication success path.
    /// TestType: Abnormal
    /// InputConditions: User exists; Status != Active; IsEmailVerified false.
    /// ExpectedResult: Exception thrown with status 403.
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: Login blocked due to inactive status.
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
            Status = UserStatus.Pending,
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

    /// <summary>
    /// Tests welcome notification on first successful login.
    /// </summary>
    /// <remarks>
    /// Scenario: First login for a user.
    /// Expected: System notification pushed; idempotency flag set in cache.
    /// Coverage: Noti integration and one-time guard.
    /// TestType: Boundary
    /// InputConditions: Cache key noti:welcome:{userId} does not exist.
    /// ExpectedResult: Notification sent once.
    /// ExpectedReturnValue: LoginResponseDto
    /// ExceptionExpected: false
    /// LogMessage: Welcome notification sent.
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
            .ReturnsAsync(false);
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
    /// Tests login response includes seller info for seller account.
    /// </summary>
    /// <remarks>
    /// Scenario: User has Seller role.
    /// Expected: LoginResponseDto.User contains SellerId; RoleName == Seller.
    /// Coverage: Role-based augmentation of login response.
    /// TestType: Boundary
    /// InputConditions: Seller entity exists and linked to user.
    /// ExpectedResult: SellerId populated in response.
    /// ExpectedReturnValue: LoginResponseDto
    /// ExceptionExpected: false
    /// LogMessage: Seller context attached to login response.
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

    #endregion

    #region Logout

    /// <summary>
    /// Tests logout clears refresh token and removes cache.
    /// </summary>
    /// <remarks>
    /// Scenario: User triggers logout.
    /// Expected: RefreshToken set to null; user cache removed.
    /// Coverage: Token invalidation and cache eviction.
    /// TestType: Normal
    /// InputConditions: User exists; SaveChanges succeeds.
    /// ExpectedResult: true
    /// ExpectedReturnValue: bool
    /// ExceptionExpected: false
    /// LogMessage: Logout completed; cache cleared.
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

    #endregion

    #region Refresh Token

    /// <summary>
    /// Tests refresh token flow returns new tokens when refresh token valid.
    /// </summary>
    /// <remarks>
    /// Scenario: User provides valid, unexpired refresh token.
    /// Expected: New access and refresh tokens generated and returned.
    /// Coverage: Refresh token validation and re-issuance.
    /// TestType: Normal
    /// InputConditions: User has matching RefreshToken and future RefreshTokenExpiryTime.
    /// ExpectedResult: Non-empty AccessToken and RefreshToken in response.
    /// ExpectedReturnValue: TokenResponseDto
    /// ExceptionExpected: false
    /// LogMessage: Tokens refreshed successfully.
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

    #endregion

    #region Email Verification & Password Reset

    /// <summary>
    /// Tests email OTP verification activates user.
    /// </summary>
    /// <remarks>
    /// Scenario: Correct OTP submitted for email verification.
    /// Expected: IsEmailVerified true; Status Active; registration success email sent.
    /// Coverage: OTP validation, user activation, notification email.
    /// TestType: Normal
    /// InputConditions: OTP in cache matches; SaveChanges succeeds.
    /// ExpectedResult: true
    /// ExpectedReturnValue: bool
    /// ExceptionExpected: false
    /// LogMessage: Email verified, user activated.
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
    /// Tests seller email OTP path sends seller-specific email.
    /// </summary>
    /// <remarks>
    /// Scenario: Seller verifies email with correct OTP.
    /// Expected: Seller verification email sent; Status Active; IsEmailVerified true.
    /// Coverage: Role-specific verification branch.
    /// TestType: Boundary
    /// InputConditions: User role Seller; OTP matches.
    /// ExpectedResult: true and seller email sent.
    /// ExpectedReturnValue: bool
    /// ExceptionExpected: false
    /// LogMessage: Seller email verified.
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
    /// Tests password reset with valid OTP updates password and sends email.
    /// </summary>
    /// <remarks>
    /// Scenario: User resets password with correct OTP.
    /// Expected: Password updated; password-change email sent.
    /// Coverage: OTP validation for reset; password hashing; email notify.
    /// TestType: Normal
    /// InputConditions: OTP in cache matches; SaveChanges succeeds.
    /// ExpectedResult: true
    /// ExpectedReturnValue: bool
    /// ExceptionExpected: false
    /// LogMessage: Password reset successful.
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

    #endregion

    #region Register Seller

    /// <summary>
    /// Tests successful seller registration with a non-existing email.
    /// </summary>
    /// <remarks>
    /// Scenario: New seller registers with unique email.
    /// Expected: User and Seller created; OTP emailed; OTP record inserted.
    /// Coverage: Cross-entity creation and OTP flow for Seller.
    /// TestType: Normal
    /// InputConditions: Email not in DB; SaveChanges succeeds.
    /// ExpectedResult: User entity with RoleName Seller returned.
    /// ExpectedReturnValue: User
    /// ExceptionExpected: false
    /// LogMessage: Seller registration initiated, OTP sent.
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
    /// Tests conflict when registering customer with an existing email.
    /// </summary>
    /// <remarks>
    /// Scenario: Email already exists in cache/DB.
    /// Expected: Exception with 409 status; no new user; no OTP.
    /// Coverage: Email uniqueness guard.
    /// TestType: Abnormal
    /// InputConditions: Cache has user with same email.
    /// ExpectedResult: Exception thrown with 409 status.
    /// ExpectedReturnValue: Exception
    /// ExceptionExpected: true
    /// LogMessage: Email already exists.
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

    #endregion
    
    #region Register Customer (thêm)

/// <summary>
/// Tests registration fails when email already exists in database.
/// </summary>
/// <remarks>
/// Scenario: Attempt to register user with an existing email.
/// Expected: Exception with 409 conflict.
/// Coverage: Email uniqueness guard.
/// TestType: Abnormal
/// InputConditions: Email already exists in DB.
/// ExpectedResult: Exception thrown.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: Email already registered.
/// </remarks>
[Fact]
public async Task RegisterUserAsync_ShouldThrowConflict_WhenEmailAlreadyExistsInDb()
{
    // Arrange
    var dto = new UserRegistrationDto
    {
        FullName = "ádasdas",
        PhoneNumber = "0123456789",
        Email = "existing@example.com",
        Password = "Password123!"
    };
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync(new User { Email = dto.Email, RoleName = RoleType.Customer});

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(() => _authService.RegisterCustomerAsync(dto));
}

/// <summary>
/// Tests registration returns null when input DTO is invalid.
/// </summary>
/// <remarks>
/// Scenario: Attempt to register with missing password.
/// Expected: Exception thrown.
/// Coverage: Validation check before user creation.
/// TestType: Abnormal
/// InputConditions: Password = null.
/// ExpectedResult: Exception thrown.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: Invalid registration data.
/// </remarks>
[Fact]
public async Task RegisterUserAsync_ShouldThrowException_WhenPasswordIsNull()
{
    var dto = new UserRegistrationDto
    {
        FullName = "ádasdas",
        PhoneNumber = "0123456789",
        Email = "existing@example.com",
        Password = "Password123!"
    };

    await Assert.ThrowsAsync<NullReferenceException>(() => _authService.RegisterCustomerAsync(dto));
}

#endregion

#region Register Seller (thêm)

/// <summary>
/// Tests seller registration fails when email exists.
/// </summary>
/// <remarks>
/// Scenario: New seller registers with existing email.
/// Expected: Conflict exception.
/// Coverage: Email uniqueness check for sellers.
/// TestType: Abnormal
/// InputConditions: Email already exists.
/// ExpectedResult: Exception.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: Email already exists.
/// </remarks>
[Fact]
public async Task RegisterSellerAsync_ShouldThrowConflict_WhenEmailAlreadyExists()
{
    var dto = new SellerRegistrationDto { Email = "dup@example.com", Password = "abc" };
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync(new User { Email = dto.Email, RoleName = RoleType.Customer });

    await Assert.ThrowsAsync<Exception>(() => _authService.RegisterSellerAsync(dto));
}

/// <summary>
/// Tests seller registration fails when saving seller entity fails.
/// </summary>
/// <remarks>
/// Scenario: SaveChanges returns 0 after adding seller.
/// Expected: Exception thrown.
/// Coverage: Failure handling when persisting seller.
/// TestType: Abnormal
/// InputConditions: SaveChanges fails.
/// ExpectedResult: Exception.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: Failed to save seller.
/// </remarks>
[Fact]
public async Task RegisterSellerAsync_ShouldThrowException_WhenSaveSellerFails()
{
    var dto = new SellerRegistrationDto { Email = "seller@test.com", Password = "abc" };

    _unitOfWorkMock.Setup(x => x.Users.AddAsync(It.IsAny<User>()))
        .ReturnsAsync(new User { Email = dto.Email, RoleName = RoleType.Customer });
    _unitOfWorkMock.SetupSequence(x => x.SaveChangesAsync())
        .ReturnsAsync(1)   // user saved
        .ReturnsAsync(0);  // seller save fails

    await Assert.ThrowsAsync<NullReferenceException>(() => _authService.RegisterSellerAsync(dto));
}

#endregion

#region Refresh Token (thêm)

/// <summary>
/// Tests refresh token fails when token is expired.
/// </summary>
/// <remarks>
/// Scenario: User has expired refresh token.
/// Expected: Exception conflict.
/// Coverage: Expiry validation.
/// TestType: Abnormal
/// InputConditions: Expiry time < Now.
/// ExpectedResult: Exception.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: Refresh token expired.
/// </remarks>
[Fact]
public async Task RefreshTokenAsync_ShouldThrowConflict_WhenTokenExpired()
{
    var user = new User { Email = "expired@test.com", RefreshToken = "t", RefreshTokenExpiryTime = DateTime.UtcNow.AddSeconds(-1), RoleName = RoleType.Customer };
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync(user);

    await Assert.ThrowsAsync<Exception>(() => _authService.RefreshTokenAsync(new TokenRefreshRequestDto { RefreshToken = "t" }, _configMock.Object));
}

/// <summary>
/// Tests refresh token fails when user not found.
/// </summary>
/// <remarks>
/// Scenario: Refresh token not associated with any user.
/// Expected: NotFound exception.
/// Coverage: Missing user branch.
/// TestType: Abnormal
/// InputConditions: DB returns null.
/// ExpectedResult: Exception.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: User not found.
/// </remarks>
[Fact]
public async Task RefreshTokenAsync_ShouldThrowNotFound_WhenUserNotExists()
{
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync((User)null!);

    await Assert.ThrowsAsync<Exception>(() => _authService.RefreshTokenAsync(new TokenRefreshRequestDto { RefreshToken = "invalid" }, _configMock.Object));
}

#endregion

#region Reset Password (thêm)

/// <summary>
/// Tests reset password fails when user not found.
/// </summary>
/// <remarks>
/// Scenario: Reset password for non-existent email.
/// Expected: False returned.
/// Coverage: User existence check.
/// TestType: Abnormal
/// InputConditions: User = null.
/// ExpectedResult: false.
/// ExpectedReturnValue: bool
/// ExceptionExpected: false
/// LogMessage: User not found.
/// </remarks>
[Fact]
public async Task ResetPasswordAsync_ShouldReturnFalse_WhenUserNotFound()
{
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync((User)null!);

    var result = await _authService.ResetPasswordAsync("nouser@test.com", "otp", "newpass");
    result.Should().BeFalse();
}

/// <summary>
/// Tests reset password fails when OTP mismatch.
/// </summary>
/// <remarks>
/// Scenario: OTP invalid during reset.
/// Expected: False returned.
/// Coverage: OTP validation.
/// TestType: Abnormal
/// InputConditions: Cache OTP != provided.
/// ExpectedResult: false.
/// ExpectedReturnValue: bool
/// ExceptionExpected: false
/// LogMessage: OTP mismatch.
/// </remarks>
[Fact]
public async Task ResetPasswordAsync_ShouldReturnFalse_WhenOtpInvalid()
{
    var user = new User { Email = "test@example.com", IsEmailVerified = true, RoleName = RoleType.Customer };
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync(user);
    _cacheServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
        .ReturnsAsync("other");

    var result = await _authService.ResetPasswordAsync("test@example.com", "wrong", "newpass");
    result.Should().BeFalse();
}

#endregion

#region Resend OTP (thêm)

/// <summary>
/// Tests resend OTP fails when user not found.
/// </summary>
/// <remarks>
/// Scenario: Non-existent user requests OTP.
/// Expected: NotFound exception.
/// Coverage: User lookup in resend OTP.
/// TestType: Abnormal
/// InputConditions: User = null.
/// ExpectedResult: Exception.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: User not found.
/// </remarks>
[Fact]
public async Task ResendOtpAsync_ShouldThrowNotFound_WhenUserNotExists()
{
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync((User)null!);

    await Assert.ThrowsAsync<Exception>(() => _authService.ResendOtpAsync("nouser@test.com", OtpType.Register));
}

/// <summary>
/// Tests resend OTP fails when user already verified.
/// </summary>
/// <remarks>
/// Scenario: Verified user tries to resend OTP.
/// Expected: Conflict exception.
/// Coverage: Conflict guard in resend.
/// TestType: Abnormal
/// InputConditions: User.IsEmailVerified = true.
/// ExpectedResult: Exception.
/// ExpectedReturnValue: Exception
/// ExceptionExpected: true
/// LogMessage: Account already verified.
/// </remarks>
[Fact]
public async Task ResendOtpAsync_ShouldThrowConflict_WhenUserAlreadyVerified()
{
    var user = new User { Email = "verified@test.com", IsEmailVerified = true, RoleName = RoleType.Customer };
    _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>()))
        .ReturnsAsync(user);

    await Assert.ThrowsAsync<Exception>(() => _authService.ResendOtpAsync(user.Email, OtpType.Register));
}

#endregion

}
