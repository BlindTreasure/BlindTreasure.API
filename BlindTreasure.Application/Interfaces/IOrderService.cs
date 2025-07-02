using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IOrderService
{
    Task CancelOrderAsync(Guid orderId);
    Task<string> CheckoutAsync(CreateCheckoutRequestDto dto);
    Task<string> CheckoutFromClientCartAsync(DirectCartCheckoutDto cartDto);
    Task DeleteOrderAsync(Guid orderId);
    Task<Pagination<OrderDto>> GetMyOrdersAsync(OrderQueryParameter param);
    Task<OrderDto> GetOrderByIdAsync(Guid orderId);
}