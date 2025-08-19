using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AddressDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;

namespace BlindTreasure.UnitTest.Services;

public class AddressServiceTests
{
    private readonly AddressService _addressService;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IGenericRepository<Address>> _addressRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public AddressServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheServiceMock = new Mock<ICacheService>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _addressRepoMock = new Mock<IGenericRepository<Address>>();

        _unitOfWorkMock.Setup(x => x.Addresses).Returns(_addressRepoMock.Object);
        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(_currentUserId);

        _addressService = new AddressService(
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _loggerServiceMock.Object,
            _claimsServiceMock.Object
        );
    }

    #region CreateAsync Tests

    /// <summary>
    /// Checks if the first address created by a user is automatically set as the default.
    /// </summary>
    /// <remarks>
    /// Scenario: A user adds their very first address to their account.
    /// Expected: The new address is created and automatically marked as their default address.
    /// Coverage: Logic for handling the first address addition.
    /// </remarks>
    [Fact]
    public async Task CreateAddressAsync_ShouldCreateAddressAndSetAsDefault_WhenItIsTheFirstAddress()
    {
        // Arrange
        var createDto = new CreateAddressDto
            { FullName = "First Address", IsDefault = false }; // IsDefault is ignored for first address
        _addressRepoMock.Setup(x => x.GetAllAsync(It.IsAny<Expression<Func<Address, bool>>>()))
            .ReturnsAsync(new List<Address>()); // No existing addresses

        Address capturedAddress = null;
        _addressRepoMock.Setup(x => x.AddAsync(It.IsAny<Address>()))
            .Callback<Address>(a => capturedAddress = a)
            .ReturnsAsync((Address a) => a);

        // Act
        await _addressService.CreateAddressAsync(createDto);

        // Assert
        capturedAddress.Should().NotBeNull();
        capturedAddress.IsDefault.Should().BeTrue();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if creating a new default address correctly removes the default status from the previous one.
    /// </summary>
    /// <remarks>
    /// Scenario: A user with an existing default address adds a new address and marks it as the new default.
    /// Expected: The new address is created and set as default, while the old default address is updated to no longer be the default.
    /// Coverage: The logic for switching the default address when a new one is added.
    /// </remarks>
    [Fact]
    public async Task CreateAddressAsync_ShouldCreateAddressAndUnsetPreviousDefault_WhenNewAddressIsDefault()
    {
        // Arrange
        var userId = _currentUserId;
        var createDto = new CreateAddressDto { FullName = "New Default Address", IsDefault = true };
        var oldDefault = new Address { Id = Guid.NewGuid(), UserId = userId, IsDefault = true };
        var existingAddresses = new List<Address> { oldDefault };

        _addressRepoMock.Setup(x => x.GetAllAsync(It.IsAny<Expression<Func<Address, bool>>>()))
            .ReturnsAsync(existingAddresses);

        // Act
        await _addressService.CreateAddressAsync(createDto);

        // Assert
        oldDefault.IsDefault.Should().BeFalse();
        _addressRepoMock.Verify(x => x.Update(oldDefault), Times.Once);
        _addressRepoMock.Verify(x => x.AddAsync(It.Is<Address>(a => a.IsDefault)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    /// <summary>
    /// Checks if a user can create a new address as non-default without changing the current default address.
    /// </summary>
    /// <remarks>
    /// Scenario: A user adds a new address but does not mark it as the new default.
    /// Expected: The new address is created successfully as a non-default address, and the existing default address remains unchanged.
    /// Coverage: Logic for adding additional, non-default addresses.
    /// </remarks>
    [Fact]
    public async Task CreateAddressAsync_ShouldCreateNonDefaultAddress_WhenAnotherDefaultExists()
    {
        // Arrange
        var userId = _currentUserId;
        var createDto = new CreateAddressDto { FullName = "New Non-Default Address", IsDefault = false };
        var oldDefault = new Address { Id = Guid.NewGuid(), UserId = userId, IsDefault = true };
        var existingAddresses = new List<Address> { oldDefault };

        _addressRepoMock.Setup(x => x.GetAllAsync(It.IsAny<Expression<Func<Address, bool>>>()))
            .ReturnsAsync(existingAddresses);

        Address capturedAddress = null;
        _addressRepoMock.Setup(x => x.AddAsync(It.IsAny<Address>()))
            .Callback<Address>(a => capturedAddress = a)
            .ReturnsAsync((Address a) => a);

        // Act
        await _addressService.CreateAddressAsync(createDto);

        // Assert
        capturedAddress.Should().NotBeNull();
        capturedAddress.IsDefault.Should().BeFalse();
        oldDefault.IsDefault.Should().BeTrue(); // The old default should not have changed
        _addressRepoMock.Verify(x => x.Update(It.IsAny<Address>()), Times.Never); // No other address should be updated
    }

    #endregion

    #region UpdateAsync Tests

    /// <summary>
    /// Checks if a user can successfully update their own address.
    /// </summary>
    /// <remarks>
    /// Scenario: A user provides new details for one of their existing addresses.
    /// Expected: The address is updated in the system with the new information.
    /// Coverage: The basic address update functionality.
    /// </remarks>
    [Fact]
    public async Task UpdateAddressAsync_ShouldUpdateAddress_WhenDataIsValidAndUserIsOwner()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var updateDto = new UpdateAddressDto { FullName = "Updated Name" };
        var existingAddress = new Address { Id = addressId, UserId = _currentUserId, FullName = "Original Name" };
        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId))
            .ReturnsAsync(existingAddress);

        // Act
        var result = await _addressService.UpdateAddressAsync(addressId, updateDto);

        // Assert
        result.Should().NotBeNull();
        result.FullName.Should().Be("Updated Name");
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to update an address that does not exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to update an address using an ID that does not exist.
    /// Expected: The system responds with a 'Not Found' error.
    /// Coverage: Error handling for updating a non-existent address.
    /// </remarks>
    [Fact]
    public async Task UpdateAddressAsync_ShouldThrowNotFound_WhenAddressDoesNotExist()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var updateDto = new UpdateAddressDto { FullName = "Updated Name" };
        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId))
            .ReturnsAsync((Address)null!);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _addressService.UpdateAddressAsync(addressId, updateDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when a user tries to update an address that doesn't belong to them.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to update an address using an ID that belongs to another user.
    /// Expected: The system responds with a 'Not Found' error, preventing unauthorized updates.
    /// Coverage: Security check to ensure users can only update their own addresses.
    /// </remarks>
    [Fact]
    public async Task UpdateAddressAsync_ShouldThrowNotFound_WhenUserIsNotOwner()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var updateDto = new UpdateAddressDto { FullName = "Updated Name" };
        var existingAddress = new Address { Id = addressId, UserId = Guid.NewGuid() }; // Different user ID
        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId))
            .ReturnsAsync(existingAddress);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _addressService.UpdateAddressAsync(addressId, updateDto));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    #endregion

    #region DeleteAsync Tests

    /// <summary>
    /// Checks if a user can successfully delete their own address.
    /// </summary>
    /// <remarks>
    /// Scenario: A user deletes one of their addresses.
    /// Expected: The address is marked as deleted (soft-deleted) in the system.
    /// Coverage: The address deletion functionality.
    /// </remarks>
    [Fact]
    public async Task DeleteAddressAsync_ShouldSoftDeleteAddress_WhenAddressExistsAndUserIsOwner()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var address = new Address { Id = addressId, UserId = _currentUserId, IsDeleted = false };
        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId)).ReturnsAsync(address);

        // Act
        var result = await _addressService.DeleteAddressAsync(addressId);

        // Assert
        result.Should().BeTrue();
        _addressRepoMock.Verify(x => x.SoftRemove(It.Is<Address>(a => a.Id == addressId)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when a user tries to delete an address that doesn't belong to them.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to delete an address using an ID that belongs to another user.
    /// Expected: The system responds with a 'Not Found' error, preventing unauthorized deletions.
    /// Coverage: Security check to ensure users can only delete their own addresses.
    /// </remarks>
    [Fact]
    public async Task DeleteAddressAsync_ShouldThrowNotFound_WhenUserIsNotOwner()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var address = new Address { Id = addressId, UserId = Guid.NewGuid() }; // Different user
        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId)).ReturnsAsync(address);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _addressService.DeleteAddressAsync(addressId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when a user tries to delete an address that does not exist.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to delete an address using an ID that does not exist.
    /// Expected: The system responds with a 'Not Found' error.
    /// Coverage: Error handling for deleting a non-existent address.
    /// </remarks>
    [Fact]
    public async Task DeleteAddressAsync_ShouldThrowNotFound_WhenAddressDoesNotExist()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId)).ReturnsAsync((Address)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _addressService.DeleteAddressAsync(addressId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    #endregion

    #region SetDefaultAsync Tests

    /// <summary>
    /// Checks if an address is correctly set as the default, and any previous default is unset.
    /// </summary>
    /// <remarks>
    /// Scenario: A user chooses one of their non-default addresses and marks it as their new default.
    /// Expected: The chosen address becomes the new default, and the address that was previously the default is updated to no longer be the default.
    /// Coverage: The logic for changing a user's default address.
    /// </remarks>
    [Fact]
    public async Task SetDefaultAddressAsync_ShouldSetAddressAsDefaultAndUnsetOthers_WhenSuccessful()
    {
        // Arrange
        var userId = _currentUserId;
        var newDefaultId = Guid.NewGuid();
        var oldDefault = new Address { Id = Guid.NewGuid(), UserId = userId, IsDefault = true };
        var newDefault = new Address { Id = newDefaultId, UserId = userId, IsDefault = false };
        var addresses = new List<Address> { oldDefault, newDefault };

        _addressRepoMock.Setup(x => x.GetByIdAsync(newDefaultId)).ReturnsAsync(newDefault);
        _addressRepoMock.Setup(x => x.GetAllAsync(It.IsAny<Expression<Func<Address, bool>>>()))
            .ReturnsAsync(new List<Address> { oldDefault });

        // Act
        var result = await _addressService.SetDefaultAddressAsync(newDefaultId);

        // Assert
        result.Should().NotBeNull();
        result.IsDefault.Should().BeTrue();
        oldDefault.IsDefault.Should().BeFalse();
        _addressRepoMock.Verify(x => x.Update(oldDefault), Times.Once);
        _addressRepoMock.Verify(x => x.Update(newDefault), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when a user tries to set an address that doesn't belong to them as default.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to set an address as default using an ID that belongs to another user.
    /// Expected: The system responds with a 'Not Found' error.
    /// Coverage: Security check to ensure users can only set their own addresses as default.
    /// </remarks>
    [Fact]
    public async Task SetDefaultAddressAsync_ShouldThrowNotFound_WhenUserIsNotOwner()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var address = new Address { Id = addressId, UserId = Guid.NewGuid() }; // Different user
        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId)).ReturnsAsync(address);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _addressService.SetDefaultAddressAsync(addressId));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    /// <summary>
    /// Checks that the system handles a request to set an already-default address as default without making unnecessary changes.
    /// </summary>
    /// <remarks>
    /// Scenario: A user tries to set their current default address as default again.
    /// Expected: The system recognizes it's already the default and completes the action without trying to update other addresses.
    /// Coverage: Graceful handling of redundant default-setting actions.
    /// </remarks>
    [Fact]
    public async Task SetDefaultAddressAsync_ShouldDoNothing_WhenAddressIsAlreadyDefault()
    {
        // Arrange
        var userId = _currentUserId;
        var addressId = Guid.NewGuid();
        var address = new Address { Id = addressId, UserId = userId, IsDefault = true };

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId)).ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.GetAllAsync(It.IsAny<Expression<Func<Address, bool>>>()))
            .ReturnsAsync(new List<Address>()); // No *other* default addresses are found

        // Act
        await _addressService.SetDefaultAddressAsync(addressId);

        // Assert
        // Verify that no other addresses were updated because none needed to be
        _addressRepoMock.Verify(x => x.Update(It.Is<Address>(a => a.Id != addressId)), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(),
            Times.AtLeastOnce); // SaveChanges is still called to update the target address
    }

    #endregion

    #region GetDefaultShippingAddressAsync Tests

    /// <summary>
    /// Checks if the system can correctly find and return a user's default shipping address.
    /// </summary>
    /// <remarks>
    /// Scenario: The system needs to retrieve the default address for a specific user.
    /// Expected: The address marked as default for that user is returned.
    /// Coverage: Retrieving a specific user's default address.
    /// </remarks>
    [Fact]
    public async Task GetDefaultShippingAddressAsync_ShouldReturnDefaultAddress_WhenOneExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var defaultAddress = new Address { Id = Guid.NewGuid(), UserId = userId, IsDefault = true };
        var addresses = new List<Address>
        {
            defaultAddress,
            new() { Id = Guid.NewGuid(), UserId = userId, IsDefault = false }
        }.AsQueryable().BuildMock();

        _addressRepoMock.Setup(x => x.GetQueryable()).Returns(addresses);

        // Act
        var result = await _addressService.GetDefaultShippingAddressAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(defaultAddress.Id);
        result.IsDefault.Should().BeTrue();
    }

    /// <summary>
    /// Checks if the system returns nothing when a user does not have a default shipping address.
    /// </summary>
    /// <remarks>
    /// Scenario: The system needs to retrieve the default address for a user, but they haven't set one.
    /// Expected: The system returns null, indicating no default address was found.
    /// Coverage: Handling cases where no default address is set.
    /// </remarks>
    [Fact]
    public async Task GetDefaultShippingAddressAsync_ShouldReturnNull_WhenNoDefaultAddressExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addresses = new List<Address>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, IsDefault = false }
        }.AsQueryable().BuildMock();

        _addressRepoMock.Setup(x => x.GetQueryable()).Returns(addresses);

        // Act
        var result = await _addressService.GetDefaultShippingAddressAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}