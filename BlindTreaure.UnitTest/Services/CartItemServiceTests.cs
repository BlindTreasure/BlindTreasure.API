using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class CartItemServiceTests
{
    private readonly CartItemService _cartItemService;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IMapperService> _mapperServiceMock;
    private readonly Mock<IProductService> _productServiceMock;
    private readonly Mock<IGenericRepository<CartItem>> _cartItemRepoMock;
    private readonly Mock<IGenericRepository<Product>> _productRepoMock;
    private readonly Mock<IGenericRepository<BlindBox>> _blindBoxRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public CartItemServiceTests()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _mapperServiceMock = new Mock<IMapperService>();
        _productServiceMock = new Mock<IProductService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cartItemRepoMock = new Mock<IGenericRepository<CartItem>>();
        _productRepoMock = new Mock<IGenericRepository<Product>>();
        _blindBoxRepoMock = new Mock<IGenericRepository<BlindBox>>();

        _unitOfWorkMock.Setup(x => x.CartItems).Returns(_cartItemRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Products).Returns(_productRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.BlindBoxes).Returns(_blindBoxRepoMock.Object);

        _claimsServiceMock.Setup(x => x.CurrentUserId).Returns(_currentUserId);

        _cartItemService = new CartItemService(
            _cacheServiceMock.Object,
            _claimsServiceMock.Object,
            _loggerServiceMock.Object,
            _mapperServiceMock.Object,
            _productServiceMock.Object,
            _unitOfWorkMock.Object
        );
    }

    #region GetCurrentUserCartAsync Tests

    /// <summary>
    /// Checks if the system correctly retrieves all active cart items for the current user.
    /// </summary>
    /// <remarks>
    /// Scenario: A user has several active items in their shopping cart.
    /// Expected: The system returns a list containing all these cart items, including details like product/blind box names and images.
    /// Coverage: Retrieving a user's entire cart, including associated product and blind box details.
    /// </remarks>
    [Fact]
    public async Task GetCurrentUserCartAsync_ShouldReturnCartItemsForCurrentUser()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItems = new List<CartItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, ProductId = Guid.NewGuid(), Quantity = 1,
                Product = new Product{Name = "Product 1", ImageUrls = new List<string>{"img1.jpg"}}},
            new() { Id = Guid.NewGuid(), UserId = userId, BlindBoxId = Guid.NewGuid(), Quantity = 2,
                BlindBox = new BlindBox{Name = "BlindBox 1", ImageUrl = "bb_img1.jpg", Description = "hehe"}}
        }.AsQueryable().BuildMock();

        _cartItemRepoMock.Setup(x => x.GetAllAsync(
            It.IsAny<Expression<Func<CartItem, bool>>>(),
            It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(cartItems.ToList());

        // Act
        var result = await _cartItemService.GetCurrentUserCartAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.First().ProductName.Should().Be("Product 1");
        result.Items.Last().BlindBoxName.Should().Be("BlindBox 1");
    }

    /// <summary>
    /// Checks if an empty cart is returned when the current user has no active cart items.
    /// </summary>
    /// <remarks>
    /// Scenario: A user has an empty shopping cart or all their cart items are marked as deleted.
    /// Expected: The system returns an empty list of cart items.
    /// Coverage: Handling empty cart scenarios correctly.
    /// </remarks>
    [Fact]
    public async Task GetCurrentUserCartAsync_ShouldReturnEmptyCart_WhenNoItems()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItems = new List<CartItem>().AsQueryable().BuildMock();

        _cartItemRepoMock.Setup(x => x.GetAllAsync(
            It.IsAny<Expression<Func<CartItem, bool>>>(),
            It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(cartItems.ToList());

        // Act
        var result = await _cartItemService.GetCurrentUserCartAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    #endregion

    #region AddToCartAsync Tests

    /// <summary>
    /// Checks if a new product is successfully added to the user's cart.
    /// </summary>
    /// <remarks>
    /// Scenario: A user adds a product to their cart that wasn't there before.
    /// Expected: The product is added as a new item in the cart, and the updated cart details are returned.
    /// Coverage: Adding a completely new item to the cart and updating total price correctly.
    /// </remarks>
    [Fact]
    public async Task AddToCartAsync_ShouldAddNewCartItem_WhenProductDoesNotExistInCart()
    {
        // Arrange
        var userId = _currentUserId;
        var productId = Guid.NewGuid();
        var addDto = new AddCartItemDto { ProductId = productId, Quantity = 1 };
        var product = new Product { Id = productId, Price = 50, Stock = 10 };

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        _cartItemRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<CartItem, bool>>>(), It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync((CartItem)null!); // Item does not exist in cart

        _cartItemRepoMock.Setup(x => x.AddAsync(It.IsAny<CartItem>()))
            .ReturnsAsync((CartItem ci) => ci); // Return the added cart item

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetCurrentUserCartAsync to return the new state of the cart
        _cartItemRepoMock.Setup(x => x.GetAllAsync(
            It.IsAny<Expression<Func<CartItem, bool>>>(),
            It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(new List<CartItem>
            {
                new() { UserId = userId, ProductId = productId, Quantity = 1, UnitPrice = 50, TotalPrice = 50,
                    Product = new Product { Name = "Added Product", ImageUrls = new List<string>{"added.jpg"} } }
            });

        // Act
        var result = await _cartItemService.AddToCartAsync(addDto);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().ProductId.Should().Be(productId);
        result.Items.First().Quantity.Should().Be(1);
        result.Items.First().TotalPrice.Should().Be(50);
        _cartItemRepoMock.Verify(x => x.AddAsync(It.IsAny<CartItem>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if the quantity of an existing cart item is correctly updated when the same product is added again.
    /// </summary>
    /// <remarks>
    /// Scenario: A user adds a product to their cart that they already have.
    /// Expected: Instead of a new cart item, the quantity of the existing cart item is increased, and the total price is updated.
    /// Coverage: Updating quantity for existing cart items and ensuring correct price calculation.
    /// </remarks>
    [Fact]
    public async Task AddToCartAsync_ShouldUpdateExistingCartItem_WhenProductAlreadyInCart()
    {
        // Arrange
        var userId = _currentUserId;
        var productId = Guid.NewGuid();
        var addDto = new AddCartItemDto { ProductId = productId, Quantity = 2 };
        var product = new Product { Id = productId, Price = 50, Stock = 10 };

        var existingCartItem = new CartItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = productId,
            Quantity = 3,
            UnitPrice = 50,
            TotalPrice = 150,
            IsDeleted = false
        };

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        _cartItemRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<CartItem, bool>>>(), It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(existingCartItem); // Item already exists

        _cartItemRepoMock.Setup(x => x.Update(It.IsAny<CartItem>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetCurrentUserCartAsync to return the new state of the cart
        _cartItemRepoMock.Setup(x => x.GetAllAsync(
            It.IsAny<Expression<Func<CartItem, bool>>>(),
            It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(new List<CartItem>
            {
                new() { UserId = userId, ProductId = productId, Quantity = 5, UnitPrice = 50, TotalPrice = 250,
                    Product = new Product { Name = "Updated Product", ImageUrls = new List<string>{"updated.jpg"} } }
            });

        // Act
        var result = await _cartItemService.AddToCartAsync(addDto);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().ProductId.Should().Be(productId);
        result.Items.First().Quantity.Should().Be(5); // 3 (original) + 2 (added)
        result.Items.First().TotalPrice.Should().Be(250); // 5 * 50
        _cartItemRepoMock.Verify(x => x.Update(It.IsAny<CartItem>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when trying to add a product with a quantity of zero or less.
    /// </summary>
    /// <remarks>
    /// Scenario: A user tries to add an item to their cart with a quantity that is not positive (e.g., 0 or -1).
    /// Expected: The system rejects the request with a 'Bad Request' error, indicating that the quantity must be greater than zero.
    /// Coverage: Input validation for cart item quantity.
    /// </remarks>
    [Fact]
    public async Task AddToCartAsync_ShouldThrowBadRequest_WhenQuantityIsZeroOrLess()
    {
        // Arrange
        var addDto = new AddCartItemDto { ProductId = Guid.NewGuid(), Quantity = 0 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.AddToCartAsync(addDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemQuantityMustBeGreaterThanZero);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when trying to add an item without specifying either a product or a blind box.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to add an item to their cart without providing a product ID or a blind box ID.
    /// Expected: The system rejects the request with a 'Bad Request' error, indicating that either a product or a blind box must be specified.
    /// Coverage: Input validation to ensure an item type is specified for cart additions.
    /// </remarks>
    [Fact]
    public async Task AddToCartAsync_ShouldThrowBadRequest_WhenNeitherProductNorBlindBoxIdIsProvided()
    {
        // Arrange
        var addDto = new AddCartItemDto { Quantity = 1 }; // No ProductId or BlindBoxId

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.AddToCartAsync(addDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemProductOrBlindBoxRequired);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to add a product that does not exist or is deleted.
    /// </summary>
    /// <remarks>
    /// Scenario: A user tries to add a product to their cart using an ID that doesn't match any active product.
    /// Expected: The system responds with a 'Not Found' error, indicating the product is unavailable.
    /// Coverage: Validating product existence and active status before adding to cart.
    /// </remarks>
    [Fact]
    public async Task AddToCartAsync_ShouldThrowNotFound_WhenProductDoesNotExistOrIsDeleted()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var addDto = new AddCartItemDto { ProductId = productId, Quantity = 1 };

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync((Product)null!); // Product not found

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.AddToCartAsync(addDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemProductNotFound);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when trying to add an out-of-stock product to the cart.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to add a quantity of a product that exceeds its current stock.
    /// Expected: The system rejects the request with a 'Bad Request' error, indicating that the product is out of stock for the requested quantity.
    /// Coverage: Preventing users from adding more items than available in stock.
    /// </remarks>
    [Fact]
    public async Task AddToCartAsync_ShouldThrowBadRequest_WhenProductIsOutOfStock()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var addDto = new AddCartItemDto { ProductId = productId, Quantity = 10 };
        var product = new Product { Id = productId, Price = 50, Stock = 5 }; // Stock is 5, but trying to add 10

        _productRepoMock.Setup(x => x.GetByIdAsync(productId))
            .ReturnsAsync(product);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.AddToCartAsync(addDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemProductOutOfStock);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to add a blind box that does not exist, is deleted, or rejected.
    /// </summary>
    /// <remarks>
    /// Scenario: A user tries to add a blind box to their cart using an ID that doesn't match any available blind box.
    /// Expected: The system responds with a 'Not Found' error, indicating the blind box is unavailable.
    /// Coverage: Validating blind box existence and status before adding to cart.
    /// </remarks>
    [Fact]
    public async Task AddToCartAsync_ShouldThrowNotFound_WhenBlindBoxDoesNotExistOrIsRejected()
    {
        // Arrange
        var blindBoxId = Guid.NewGuid();
        var addDto = new AddCartItemDto { BlindBoxId = blindBoxId, Quantity = 1 };

        _blindBoxRepoMock.Setup(x => x.GetByIdAsync(blindBoxId))
            .ReturnsAsync((BlindBox)null!); // Blind box not found

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.AddToCartAsync(addDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemBlindBoxNotFoundOrRejected);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    #endregion

    #region UpdateCartItemAsync Tests

    /// <summary>
    /// Checks if a cart item's quantity is successfully updated when valid data is provided.
    /// </summary>
    /// <remarks>
    /// Scenario: A user changes the quantity of an item already in their cart to a new positive value.
    /// Expected: The quantity of the cart item is updated, and its total price reflects the new quantity. The updated cart details are then returned.
    /// Coverage: Updating existing cart item quantities and recalculating totals.
    /// </remarks>
    [Fact]
    public async Task UpdateCartItemAsync_ShouldUpdateCartItem_WhenValidData()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var updateDto = new UpdateCartItemDto { CartItemId = cartItemId, Quantity = 5 };

        var existingProduct = new Product { Id = productId, Price = 100, Stock = 10 };
        var existingCartItem = new CartItem
        {
            Id = cartItemId,
            UserId = userId,
            ProductId = productId,
            Quantity = 2,
            UnitPrice = 100,
            TotalPrice = 200,
            IsDeleted = false,
            Product = existingProduct
        };

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId, It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(existingCartItem);

        _cartItemRepoMock.Setup(x => x.Update(It.IsAny<CartItem>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetCurrentUserCartAsync
        _cartItemRepoMock.Setup(x => x.GetAllAsync(
            It.IsAny<Expression<Func<CartItem, bool>>>(),
            It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(new List<CartItem>
            {
                new() { UserId = userId, ProductId = productId, Quantity = 5, UnitPrice = 100, TotalPrice = 500,
                    Product = new Product{Name = "Product X", ImageUrls = new List<string>{"x.jpg"}} }
            });

        // Act
        var result = await _cartItemService.UpdateCartItemAsync(updateDto);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Quantity.Should().Be(5);
        result.Items.First().TotalPrice.Should().Be(500);
        _cartItemRepoMock.Verify(x => x.Update(It.IsAny<CartItem>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if a cart item is removed (soft-deleted) when its quantity is updated to zero or less.
    /// </summary>
    /// <remarks>
    /// Scenario: A user updates the quantity of an item in their cart to 0 or a negative number.
    /// Expected: The cart item is marked as deleted in the system, effectively removing it from the user's active cart.
    /// Coverage: Automatically removing items from the cart when their quantity becomes invalid.
    /// </remarks>
    [Fact]
    public async Task UpdateCartItemAsync_ShouldRemoveCartItem_WhenQuantityIsZeroOrLess()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItemId = Guid.NewGuid();
        var updateDto = new UpdateCartItemDto { CartItemId = cartItemId, Quantity = 0 };

        var existingCartItem = new CartItem
        {
            Id = cartItemId,
            UserId = userId,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            IsDeleted = false
        };

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId, It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(existingCartItem);

        _cartItemRepoMock.Setup(x => x.Update(It.IsAny<CartItem>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetCurrentUserCartAsync
        _cartItemRepoMock.Setup(x => x.GetAllAsync(
            It.IsAny<Expression<Func<CartItem, bool>>>(),
            It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(new List<CartItem>());

        // Act
        var result = await _cartItemService.UpdateCartItemAsync(updateDto);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        existingCartItem.IsDeleted.Should().BeTrue(); // Verify soft delete
        _cartItemRepoMock.Verify(x => x.Update(It.IsAny<CartItem>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to update a cart item that doesn't exist or doesn't belong to the user.
    /// </summary>
    /// <remarks>
    /// Scenario: A user tries to update a cart item using an ID that either doesn't exist or is for an item in another user's cart.
    /// Expected: The system responds with a 'Not Found' error, indicating the cart item is inaccessible or non-existent.
    /// Coverage: Validating cart item ownership and existence before allowing updates.
    /// </remarks>
    [Fact]
    public async Task UpdateCartItemAsync_ShouldThrowNotFound_WhenCartItemNotFoundOrNotOwned()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItemId = Guid.NewGuid();
        var updateDto = new UpdateCartItemDto { CartItemId = cartItemId, Quantity = 1 };

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId, It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync((CartItem)null!); // Cart item not found

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.UpdateCartItemAsync(updateDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemNotFound);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    /// <summary>
    /// Checks if a 'Bad Request' error occurs when trying to update a product's quantity in the cart to exceed its stock.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to increase the quantity of a product in their cart beyond the available stock.
    /// Expected: The system rejects the update with a 'Bad Request' error, indicating insufficient stock.
    /// Coverage: Preventing cart items from exceeding available product stock during updates.
    /// </remarks>
    [Fact]
    public async Task UpdateCartItemAsync_ShouldThrowBadRequest_WhenProductOutOfStock()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var updateDto = new UpdateCartItemDto { CartItemId = cartItemId, Quantity = 15 }; // Requesting more than stock

        var existingProduct = new Product { Id = productId, Price = 100, Stock = 10 }; // Only 10 in stock
        var existingCartItem = new CartItem
        {
            Id = cartItemId,
            UserId = userId,
            ProductId = productId,
            Quantity = 2,
            IsDeleted = false,
            Product = existingProduct
        };

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId, It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(existingCartItem);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.UpdateCartItemAsync(updateDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemProductOutOfStock);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(400);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs if the product associated with a cart item is missing or deleted during an update.
    /// </summary>
    /// <remarks>
    /// Scenario: A cart item is found, but its linked product is either deleted or no longer exists in the system.
    /// Expected: The system responds with a 'Not Found' error, as the cart item is no longer valid without its product.
    /// Coverage: Ensuring the integrity of cart items by checking the existence of linked products during updates.
    /// </remarks>
    [Fact]
    public async Task UpdateCartItemAsync_ShouldThrowNotFound_WhenProductMissingOrDeleted()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var updateDto = new UpdateCartItemDto { CartItemId = cartItemId, Quantity = 5 };

        var existingCartItem = new CartItem
        {
            Id = cartItemId,
            UserId = userId,
            ProductId = productId,
            Quantity = 2,
            IsDeleted = false,
            Product = null! // Product is null, simulating missing product
        };

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId, It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(existingCartItem);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.UpdateCartItemAsync(updateDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemProductNotFound);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs if the blind box associated with a cart item is missing or deleted during an update.
    /// </summary>
    /// <remarks>
    /// Scenario: A cart item is found, but its linked blind box is either deleted or no longer exists in the system.
    /// Expected: The system responds with a 'Not Found' error, as the cart item is no longer valid without its blind box.
    /// Coverage: Ensuring the integrity of cart items by checking the existence of linked blind boxes during updates.
    /// </remarks>
    [Fact]
    public async Task UpdateCartItemAsync_ShouldThrowNotFound_WhenBlindBoxMissingOrDeleted()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItemId = Guid.NewGuid();
        var blindBoxId = Guid.NewGuid();
        var updateDto = new UpdateCartItemDto { CartItemId = cartItemId, Quantity = 5 };

        var existingCartItem = new CartItem
        {
            Id = cartItemId,
            UserId = userId,
            BlindBoxId = blindBoxId,
            Quantity = 2,
            IsDeleted = false,
            BlindBox = null! // BlindBox is null, simulating missing blind box
        };

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId, It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(existingCartItem);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.UpdateCartItemAsync(updateDto));
        exception.Message.Should().Contain(ErrorMessages.CartItemBlindBoxNotFound);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    #endregion

    #region RemoveCartItemAsync Tests

    /// <summary>
    /// Checks if a specific cart item is successfully removed (soft-deleted) from the user's cart.
    /// </summary>
    /// <remarks>
    /// Scenario: A user decides to remove a particular item from their shopping cart.
    /// Expected: The specified cart item is marked as deleted in the system, effectively disappearing from the user's active cart. The updated cart details are then returned.
    /// Coverage: Removing individual items from the cart and reflecting the change in the overall cart view.
    /// </remarks>
    [Fact]
    public async Task RemoveCartItemAsync_ShouldRemoveCartItem_WhenItemExistsAndIsOwned()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItemId = Guid.NewGuid();
        var existingCartItem = new CartItem
        {
            Id = cartItemId,
            UserId = userId,
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            IsDeleted = false
        };

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId))
            .ReturnsAsync(existingCartItem);

        _cartItemRepoMock.Setup(x => x.Update(It.IsAny<CartItem>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Mock GetCurrentUserCartAsync to return an empty cart after removal
        _cartItemRepoMock.Setup(x => x.GetAllAsync(
            It.IsAny<Expression<Func<CartItem, bool>>>(),
            It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .ReturnsAsync(new List<CartItem>());

        // Act
        var result = await _cartItemService.RemoveCartItemAsync(cartItemId);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        existingCartItem.IsDeleted.Should().BeTrue(); // Verify soft delete
        _cartItemRepoMock.Verify(x => x.Update(It.IsAny<CartItem>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if a 'Not Found' error occurs when trying to remove a cart item that doesn't exist or doesn't belong to the user.
    /// </summary>
    /// <remarks>
    /// Scenario: A user attempts to remove a cart item using an ID that is either non-existent or belongs to another user's cart.
    /// Expected: The system responds with a 'Not Found' error, indicating the cart item cannot be removed.
    /// Coverage: Validating cart item ownership and existence before allowing removal.
    /// </remarks>
    [Fact]
    public async Task RemoveCartItemAsync_ShouldThrowNotFound_WhenItemNotFoundOrNotOwned()
    {
        // Arrange
        var cartItemId = Guid.NewGuid();

        _cartItemRepoMock.Setup(x => x.GetByIdAsync(cartItemId))
            .ReturnsAsync((CartItem)null!); // Item not found

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _cartItemService.RemoveCartItemAsync(cartItemId));
        exception.Message.Should().Contain(ErrorMessages.CartItemNotFound);
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    #endregion

    #region ClearCartAsync Tests

    /// <summary>
    /// Checks if all active cart items for the current user are successfully removed.
    /// </summary>
    /// <remarks>
    /// Scenario: A user chooses to clear their entire shopping cart.
    /// Expected: All active items in the user's cart are marked as deleted in the system.
    /// Coverage: Clearing the entire cart for a user.
    /// </remarks>
    [Fact]
    public async Task ClearCartAsync_ShouldClearAllCartItemsForCurrentUser()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItems = new List<CartItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, ProductId = Guid.NewGuid(), Quantity = 1, IsDeleted = false },
            new() { Id = Guid.NewGuid(), UserId = userId, BlindBoxId = Guid.NewGuid(), Quantity = 2, IsDeleted = false }
        }.AsQueryable().BuildMock();

        _cartItemRepoMock.Setup(x => x.GetAllAsync(It.IsAny<Expression<Func<CartItem, bool>>>()))
            .ReturnsAsync(cartItems.ToList());

        _cartItemRepoMock.Setup(x => x.UpdateRange(It.IsAny<List<CartItem>>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _cartItemService.ClearCartAsync();

        // Assert
        _cartItemRepoMock.Verify(x => x.UpdateRange(It.Is<List<CartItem>>(items => items.All(i => i.IsDeleted))),
            Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region UpdateCartAfterCheckoutAsync Tests

    /// <summary>
    /// Checks if cart item quantities are correctly reduced after a successful checkout.
    /// </summary>
    /// <remarks>
    /// Scenario: A user completes a purchase, and the system needs to update their cart to reflect the bought items.
    /// Expected: Quantities of purchased items in the cart are reduced, and items fully bought (quantity becomes zero) are marked as deleted. The changes are saved.
    /// Coverage: Post-checkout cart synchronization, ensuring cart accuracy after transactions.
    /// </remarks>
    [Fact]
    public async Task UpdateCartAfterCheckoutAsync_ShouldUpdateQuantitiesAndRemoveItems_WhenCheckoutSuccessful()
    {
        // Arrange
        var userId = _currentUserId;
        var productId1 = Guid.NewGuid();
        var blindBoxId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();

        var cartItems = new List<CartItem>
        {
            new() { UserId = userId, ProductId = productId1, Quantity = 5, UnitPrice = 10, TotalPrice = 50, IsDeleted = false },
            new() { UserId = userId, BlindBoxId = blindBoxId1, Quantity = 3, UnitPrice = 20, TotalPrice = 60, IsDeleted = false },
            new() { UserId = userId, ProductId = productId2, Quantity = 2, UnitPrice = 30, TotalPrice = 60, IsDeleted = false }
        };

        var checkoutItems = new List<OrderService.CheckoutItem>
        {
            new() { ProductId = productId1, Quantity = 5 }, // Fully purchased
            new() { BlindBoxId = blindBoxId1, Quantity = 1 } // Partially purchased
        };

        _cartItemRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<CartItem, bool>>>(), It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .Returns<Expression<Func<CartItem, bool>>, Expression<Func<CartItem, object>>[]>((predicate, includes) => Task.FromResult(cartItems.AsQueryable().FirstOrDefault(predicate)));

        _cartItemRepoMock.Setup(x => x.Update(It.IsAny<CartItem>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _cartItemService.UpdateCartAfterCheckoutAsync(userId, checkoutItems);

        // Assert
        var updatedProduct1CartItem = cartItems.First(ci => ci.ProductId == productId1);
        updatedProduct1CartItem.IsDeleted.Should().BeTrue(); // Should be deleted

        var updatedBlindBox1CartItem = cartItems.First(ci => ci.BlindBoxId == blindBoxId1);
        updatedBlindBox1CartItem.Quantity.Should().Be(2); // 3 - 1 = 2
        updatedBlindBox1CartItem.TotalPrice.Should().Be(40); // 2 * 20 = 40
        updatedBlindBox1CartItem.IsDeleted.Should().BeFalse(); // Should not be deleted

        var product2CartItem = cartItems.First(ci => ci.ProductId == productId2);
        product2CartItem.Quantity.Should().Be(2); // Should remain unchanged
        product2CartItem.IsDeleted.Should().BeFalse(); // Should not be deleted

        _cartItemRepoMock.Verify(x => x.Update(It.IsAny<CartItem>()), Times.Exactly(2)); // Product1 and BlindBox1 updated
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks that the cart remains unchanged if no items from the checkout list are found in the cart.
    /// </summary>
    /// <remarks>
    /// Scenario: A checkout occurs, but none of the purchased items are present in the user's cart.
    /// Expected: No changes are made to the user's cart items in the database.
    /// Coverage: Ensures that only relevant cart items are modified after checkout.
    /// </remarks>
    [Fact]
    public async Task UpdateCartAfterCheckoutAsync_ShouldNotChangeCart_WhenNoMatchingItems()
    {
        // Arrange
        var userId = _currentUserId;
        var cartItems = new List<CartItem>
        {
            new() { UserId = userId, ProductId = Guid.NewGuid(), Quantity = 5, IsDeleted = false }
        };

        var nonMatchingCheckoutItems = new List<OrderService.CheckoutItem>
        {
            new() { ProductId = Guid.NewGuid(), Quantity = 1 }, // Different product ID
            new() { BlindBoxId = Guid.NewGuid(), Quantity = 1 } // Different blind box ID
        };

        _cartItemRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<CartItem, bool>>>(), It.IsAny<Expression<Func<CartItem, object>>[]>()))
            .Returns<Expression<Func<CartItem, bool>>, Expression<Func<CartItem, object>>[]>((predicate, includes) => Task.FromResult(cartItems.AsQueryable().FirstOrDefault(predicate)));

        _cartItemRepoMock.Setup(x => x.Update(It.IsAny<CartItem>()))
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _cartItemService.UpdateCartAfterCheckoutAsync(userId, nonMatchingCheckoutItems);

        // Assert
        _cartItemRepoMock.Verify(x => x.Update(It.IsAny<CartItem>()), Times.Never); // No updates should occur
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never); // No changes should be saved
    }

    #endregion
}