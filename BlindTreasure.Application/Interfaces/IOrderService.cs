using BlindTreasure.Domain.DTOs.OrderDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IOrderService
{
    Task CancelOrderAsync(Guid orderId);
    Task<OrderDto> CheckoutAsync(CreateOrderDto dto);
    Task DeleteOrderAsync(Guid orderId);
    Task<List<OrderDto>> GetMyOrdersAsync();
    Task<OrderDto> GetOrderByIdAsync(Guid orderId);
}