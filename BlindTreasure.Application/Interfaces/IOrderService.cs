using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IOrderService
{
    Task CancelOrderAsync(Guid orderId);
    Task<string> CheckoutAsync(CreateOrderDto dto);
    Task<string> CheckoutFromClientCartAsync(DirectCartCheckoutDto cartDto);
    Task DeleteOrderAsync(Guid orderId);
    Task<List<OrderDto>> GetMyOrdersAsync();
    Task<OrderDto> GetOrderByIdAsync(Guid orderId);
}