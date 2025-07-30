using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using static BlindTreasure.Application.Services.OrderService;

namespace BlindTreasure.Application.Services;

public class CartItemService : ICartItemService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapper;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;

    public CartItemService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IMapperService mapper,
        IProductService productService,
        IUnitOfWork unitOfWork)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _mapper = mapper;
        _productService = productService;
        _unitOfWork = unitOfWork;
    }


    // Lấy toàn bộ cart của user hiện tại
    public async Task<CartDto> GetCurrentUserCartAsync()
    {
        var userId = _claimsService.CurrentUserId;
        var cartItems = await _unitOfWork.CartItems.GetAllAsync(
            c => c.UserId == userId && !c.IsDeleted,
            c => c.Product,
            c => c.BlindBox
        );

        var dtos = cartItems.Select(c => new CartItemDto
        {
            Id = c.Id,
            ProductId = c.ProductId,
            ProductName = c.Product?.Name,
            ProductImages = c.Product?.ImageUrls,
            BlindBoxId = c.BlindBoxId,
            BlindBoxName = c.BlindBox?.Name,
            BlindBoxImage = c.BlindBox?.ImageUrl,
            Quantity = c.Quantity,
            UnitPrice = c.UnitPrice,
            TotalPrice = c.TotalPrice,
            CreatedAt = c.CreatedAt
        }).ToList();

        return new CartDto { Items = dtos };
    }

    // Thêm sản phẩm hoặc blindbox vào cart
    public async Task<CartDto> AddToCartAsync(AddCartItemDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        if (dto.Quantity <= 0)
            throw ErrorHelper.BadRequest(ErrorMessages.CartItemQuantityMustBeGreaterThanZero);

        if (dto.ProductId == null && dto.BlindBoxId == null)
            throw ErrorHelper.BadRequest(ErrorMessages.CartItemProductOrBlindBoxRequired);

        decimal unitPrice;
        if (dto.ProductId.HasValue)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId.Value);
            if (product == null || product.IsDeleted)
                throw ErrorHelper.NotFound(ErrorMessages.CartItemProductNotFound);
            if (product.Stock < dto.Quantity)
                throw ErrorHelper.BadRequest(ErrorMessages.CartItemProductOutOfStock);
            unitPrice = product.Price;
        }
        else
        {
            var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(dto.BlindBoxId.Value);
            if (blindBox == null || blindBox.IsDeleted || blindBox.Status == BlindBoxStatus.Rejected)
                throw ErrorHelper.NotFound(ErrorMessages.CartItemBlindBoxNotFoundOrRejected);
            unitPrice = blindBox.Price;
        }

        var existed = await _unitOfWork.CartItems.FirstOrDefaultAsync(c => c.UserId == userId
                                                                           && c.ProductId == dto.ProductId
                                                                           && c.BlindBoxId == dto.BlindBoxId
                                                                           && !c.IsDeleted
        );

        if (existed != null)
        {
            existed.Quantity += dto.Quantity;
            existed.UnitPrice = unitPrice;
            existed.TotalPrice = existed.Quantity * unitPrice;
            existed.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CartItems.Update(existed);
        }
        else
        {
            var cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                BlindBoxId = dto.BlindBoxId,
                Quantity = dto.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = dto.Quantity * unitPrice,
                AddedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.CartItems.AddAsync(cartItem);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.Success("[AddToCartAsync] Add to cart successful.");
        return await GetCurrentUserCartAsync();
    }

    // Cập nhật số lượng cart item
    public async Task<CartDto> UpdateCartItemAsync(UpdateCartItemDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var cartItem = await _unitOfWork.CartItems.GetByIdAsync(dto.CartItemId, c => c.Product, c => c.BlindBox);
        if (cartItem == null || cartItem.IsDeleted || cartItem.UserId != userId)
            throw ErrorHelper.NotFound(ErrorMessages.CartItemNotFound);

        if (dto.Quantity <= 0)
        {
            cartItem.IsDeleted = true;
            cartItem.DeletedAt = DateTime.UtcNow;
            await _unitOfWork.CartItems.Update(cartItem);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.Success("[UpdateCartItemAsync] Cart item removed because quantity <= 0.");
            return await GetCurrentUserCartAsync();
        }

        if (cartItem.ProductId.HasValue)
        {
            var product = cartItem.Product;
            if (product == null || product.IsDeleted)
                throw ErrorHelper.NotFound(ErrorMessages.CartItemProductNotFound);
            if (product.Stock < dto.Quantity)
                throw ErrorHelper.BadRequest(ErrorMessages.CartItemProductOutOfStock);
            cartItem.UnitPrice = product.Price;
        }
        else if (cartItem.BlindBoxId.HasValue)
        {
            var blindBox = cartItem.BlindBox;
            if (blindBox == null || blindBox.IsDeleted)
                throw ErrorHelper.NotFound(ErrorMessages.CartItemBlindBoxNotFound);
            cartItem.UnitPrice = blindBox.Price;
        }

        cartItem.Quantity = dto.Quantity;
        cartItem.TotalPrice = cartItem.UnitPrice * dto.Quantity;
        cartItem.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CartItems.Update(cartItem);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.Success("[UpdateCartItemAsync] Cart updated successfully.");
        return await GetCurrentUserCartAsync();
    }

    // Xóa 1 item khỏi cart
    public async Task<CartDto> RemoveCartItemAsync(Guid cartItemId)
    {
        var userId = _claimsService.CurrentUserId;
        var cartItem = await _unitOfWork.CartItems.GetByIdAsync(cartItemId);
        if (cartItem == null || cartItem.IsDeleted || cartItem.UserId != userId)
            throw ErrorHelper.NotFound(ErrorMessages.CartItemNotFound);

        cartItem.IsDeleted = true;
        cartItem.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.CartItems.Update(cartItem);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.Success("[RemoveCartItemAsync] Cart item removed successfully.");
        return await GetCurrentUserCartAsync();
    }

    // Xóa toàn bộ cart của user
    public async Task ClearCartAsync()
    {
        var userId = _claimsService.CurrentUserId;
        var cartItems = await _unitOfWork.CartItems.GetAllAsync(c => c.UserId == userId && !c.IsDeleted);
        foreach (var item in cartItems)
        {
            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
        }

        await _unitOfWork.CartItems.UpdateRange(cartItems);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.Success("[ClearCartAsync] Đã xóa toàn bộ giỏ hàng.");
    }

    public async Task UpdateCartAfterCheckoutAsync(Guid userId, List<CheckoutItem> checkoutItems)
    {
        var changesMade = false;
        foreach (var item in checkoutItems)
        {
            // Tìm cart item theo user, productId, blindBoxId
            var cartItem = await _unitOfWork.CartItems.FirstOrDefaultAsync(c => c.UserId == userId
                && c.ProductId == item.ProductId
                && c.BlindBoxId == item.BlindBoxId
                && !c.IsDeleted
            );

            if (cartItem != null)
            {
                cartItem.Quantity -= item.Quantity;
                if (cartItem.Quantity <= 0)
                {
                    cartItem.IsDeleted = true;
                    cartItem.DeletedAt = DateTime.UtcNow;
                }
                else
                {
                    cartItem.TotalPrice = cartItem.Quantity * cartItem.UnitPrice;
                    cartItem.UpdatedAt = DateTime.UtcNow;
                }

                await _unitOfWork.CartItems.Update(cartItem);
                changesMade = true;
            }
        }

        if (changesMade)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }
}