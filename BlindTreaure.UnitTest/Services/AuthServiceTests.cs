using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using System;
using System.Threading.Tasks;
using FluentAssertions;

namespace BlindTreaure.UnitTest.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<ICacheService> _cacheServiceMock;
        private readonly Mock<ILoggerService> _loggerServiceMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IGenericRepository<OtpVerification>> _otpVerificationRepoMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _emailServiceMock = new Mock<IEmailService>();
            _cacheServiceMock = new Mock<ICacheService>();
            _loggerServiceMock = new Mock<ILoggerService>();
            _configMock = new Mock<IConfiguration>();
            _otpVerificationRepoMock = new Mock<IGenericRepository<OtpVerification>>();

            //// Logger mock
            //_loggerServiceMock.Setup(x => x.Info(It.IsAny<string>()));
            //_loggerServiceMock.Setup(x => x.Success(It.IsAny<string>()));
            //_loggerServiceMock.Setup(x => x.Warn(It.IsAny<string>()));
            //_loggerServiceMock.Setup(x => x.Error(It.IsAny<string>()));

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
                _emailServiceMock.Object
            );
        }

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
            _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync((User)null!);
            _unitOfWorkMock.Setup(x => x.Users.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);
            _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);
            _emailServiceMock.Setup(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _authService.RegisterUserAsync(registrationDto);

            // Assert
            result.Should().NotBeNull();
            result!.Email.Should().Be(registrationDto.Email);
            _unitOfWorkMock.Verify(x => x.Users.AddAsync(It.IsAny<User>()), Times.Once);
            _emailServiceMock.Verify(x => x.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
            _otpVerificationRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpVerification>()), Times.Once);
        }

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
                Password = new BlindTreasure.Application.Utils.PasswordHasher().HashPassword(loginDto.Password!),
                FullName = "Test User",
                Status = UserStatus.Active,
                RoleName = RoleType.Customer,
                IsEmailVerified = true,
                DateOfBirth = new DateTime(2000, 1, 1)
            };

            _cacheServiceMock.Setup(x => x.GetAsync<User>(It.IsAny<string>()))
                .ReturnsAsync((User)null!);
            _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
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

            _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
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

            _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(user);
            _unitOfWorkMock.Setup(x => x.Users.Update(It.IsAny<User>()))
                .ReturnsAsync(true);
            _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _authService.RefreshTokenAsync(new TokenRefreshRequestDto { RefreshToken = refreshToken }, _configMock.Object);

            // Assert
            result.Should().NotBeNull();
            result!.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
        }

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

            _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
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

            _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
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

            _unitOfWorkMock.Setup(x => x.Users.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
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
    }
}
