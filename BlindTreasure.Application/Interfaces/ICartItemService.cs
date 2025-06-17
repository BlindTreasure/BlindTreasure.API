using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.CartItemDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface ICartItemService
{
    Task<CartDto> AddToCartAsync(AddCartItemDto dto);
    Task ClearCartAsync();
    Task<CartDto> GetCurrentUserCartAsync();
    Task<CartDto> RemoveCartItemAsync(Guid cartItemId);
    Task UpdateCartAfterCheckoutAsync(Guid userId, List<OrderService.CheckoutItem> checkoutItems);
    Task<CartDto> UpdateCartItemAsync(UpdateCartItemDto dto);
}