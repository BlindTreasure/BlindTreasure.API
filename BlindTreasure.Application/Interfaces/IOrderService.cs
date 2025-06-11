using BlindTreasure.Domain.DTOs.OrderDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface IOrderService
    {
        Task CancelOrderAsync(Guid orderId);
        Task<OrderDto> CheckoutAsync(CreateOrderDto dto);
        Task DeleteOrderAsync(Guid orderId);
        Task<List<OrderDto>> GetMyOrdersAsync();
        Task<OrderDto> GetOrderByIdAsync(Guid orderId);
    }
}
