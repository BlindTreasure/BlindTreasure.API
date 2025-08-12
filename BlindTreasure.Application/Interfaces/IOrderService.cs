using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IOrderService
{
    Task CancelGroupOrderPaymentAsync(Guid checkoutGroupId);
    Task CancelOrderAsync(Guid orderId);
    Task CancelOrderPaymentAsync(Guid orderId);
    Task<MultiOrderCheckoutResultDto> CheckoutAsync(CreateCheckoutRequestDto dto);
    Task<MultiOrderCheckoutResultDto> CheckoutFromClientCartAsync(DirectCartCheckoutDto cartDto);
    Task DeleteOrderAsync(Guid orderId);
    Task<Pagination<OrderDetailDto>> GetMyOrderDetailsAsync(OrderDetailQueryParameter param);
    Task<Pagination<OrderDto>> GetMyOrdersAsync(OrderQueryParameter param);
    Task<OrderDto> GetOrderByIdAsync(Guid orderId);

    Task<List<ShipmentCheckoutResponseDTO>> PreviewShippingCheckoutAsync(List<CartSellerItemDto> items,
        bool? IsPreview = false);
}