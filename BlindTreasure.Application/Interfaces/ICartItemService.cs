using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.CartItemDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface ICartItemService
{
    /// <summary>
    ///     Thêm sản phẩm hoặc blind box vào giỏ hàng của user hiện tại.
    /// </summary>
    /// <param name="dto">Thông tin sản phẩm/blind box và số lượng cần thêm.</param>
    /// <returns>Giỏ hàng sau khi thêm.</returns>
    Task<CartDto> AddToCartAsync(AddCartItemDto dto);

    /// <summary>
    ///     Xóa toàn bộ sản phẩm trong giỏ hàng của user hiện tại.
    /// </summary>
    Task ClearCartAsync();

    /// <summary>
    ///     Lấy giỏ hàng hiện tại của user đang đăng nhập.
    /// </summary>
    /// <returns>Thông tin giỏ hàng chi tiết theo từng seller.</returns>
    Task<CartDto> GetCurrentUserCartAsync();

    /// <summary>
    ///     Xóa một item cụ thể khỏi giỏ hàng.
    /// </summary>
    /// <param name="cartItemId">ID của item trong giỏ hàng.</param>
    /// <returns>Giỏ hàng sau khi xóa.</returns>
    Task<CartDto> RemoveCartItemAsync(Guid cartItemId);

    /// <summary>
    ///     Cập nhật giỏ hàng sau khi checkout (giảm số lượng hoặc xóa item đã mua).
    /// </summary>
    /// <param name="userId">ID của user đã checkout.</param>
    /// <param name="checkoutItems">Danh sách item đã được thanh toán.</param>
    Task UpdateCartAfterCheckoutAsync(Guid userId, List<OrderService.CheckoutItem> checkoutItems);

    /// <summary>
    ///     Cập nhật số lượng của một item trong giỏ hàng.
    /// </summary>
    /// <param name="dto">Thông tin item cần cập nhật.</param>
    /// <returns>Giỏ hàng sau khi cập nhật.</returns>
    Task<CartDto> UpdateCartItemAsync(UpdateCartItemDto dto);
}