using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;

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
            throw ErrorHelper.BadRequest("Số lượng phải lớn hơn 0.");

        if (dto.ProductId == null && dto.BlindBoxId == null)
            throw ErrorHelper.BadRequest("Phải chọn sản phẩm hoặc blind box.");

        // Kiểm tra tồn tại và lấy giá
        decimal unitPrice;
        if (dto.ProductId.HasValue)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId.Value);
            if (product == null || product.IsDeleted)
                throw ErrorHelper.NotFound("Sản phẩm không tồn tại.");
            if (product.Stock < dto.Quantity)
                throw ErrorHelper.BadRequest("Sản phẩm không đủ tồn kho.");
            unitPrice = product.Price;
        }
        else
        {
            var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(dto.BlindBoxId.Value);
            if (blindBox == null || blindBox.IsDeleted)
                throw ErrorHelper.NotFound("Blind box không tồn tại.");
            unitPrice = blindBox.Price;
        }

        // Kiểm tra đã có trong cart chưa
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
        _loggerService.Success("[AddToCartAsync] Thêm vào giỏ hàng thành công.");
        return await GetCurrentUserCartAsync();
    }

    // Cập nhật số lượng cart item
    public async Task<CartDto> UpdateCartItemAsync(UpdateCartItemDto dto)
    {
        var userId = _claimsService.CurrentUserId;
        var cartItem = await _unitOfWork.CartItems.GetByIdAsync(dto.CartItemId, c => c.Product, c => c.BlindBox);
        if (cartItem == null || cartItem.IsDeleted || cartItem.UserId != userId)
            throw ErrorHelper.NotFound("Cart item không tồn tại.");

        if (dto.Quantity <= 0)
            throw ErrorHelper.BadRequest("Số lượng phải lớn hơn 0.");

        // Kiểm tra tồn kho nếu là product
        if (cartItem.ProductId.HasValue)
        {
            var product = cartItem.Product;
            if (product == null || product.IsDeleted)
                throw ErrorHelper.NotFound("Sản phẩm không tồn tại.");
            if (product.Stock < dto.Quantity)
                throw ErrorHelper.BadRequest("Sản phẩm không đủ tồn kho.");
            cartItem.UnitPrice = product.Price;
        }
        else if (cartItem.BlindBoxId.HasValue)
        {
            var blindBox = cartItem.BlindBox;
            if (blindBox == null || blindBox.IsDeleted)
                throw ErrorHelper.NotFound("Blind box không tồn tại.");
            cartItem.UnitPrice = blindBox.Price;
        }

        cartItem.Quantity = dto.Quantity;
        cartItem.TotalPrice = cartItem.UnitPrice * dto.Quantity;
        cartItem.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CartItems.Update(cartItem);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.Success("[UpdateCartItemAsync] Cập nhật giỏ hàng thành công.");
        return await GetCurrentUserCartAsync();
    }

    // Xóa 1 item khỏi cart
    public async Task<CartDto> RemoveCartItemAsync(Guid cartItemId)
    {
        var userId = _claimsService.CurrentUserId;
        var cartItem = await _unitOfWork.CartItems.GetByIdAsync(cartItemId);
        if (cartItem == null || cartItem.IsDeleted || cartItem.UserId != userId)
            throw ErrorHelper.NotFound("Cart item không tồn tại.");

        cartItem.IsDeleted = true;
        cartItem.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.CartItems.Update(cartItem);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.Success("[RemoveCartItemAsync] Xóa item khỏi giỏ hàng thành công.");
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
}