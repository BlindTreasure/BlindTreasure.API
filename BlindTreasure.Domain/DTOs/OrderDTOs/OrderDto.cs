using BlindTreasure.Domain.DTOs.PaymentDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class OrderDto
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime PlacedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public OrderAddressDto? ShippingAddress { get; set; }
    public List<OrderDetailDto> Details { get; set; }
    public PaymentDto? Payment { get; set; } // Thêm dòng này
    public decimal FinalAmount { get; set; }
    public decimal? TotalShippingFee { get; set; } = 0; // Tổng phí vận chuyển của từ shipment của các items thuộc Order
    public Guid? CheckoutGroupId { get; set; } // MỚI: Group ID để nhóm các order cùng checkout
    public Guid? SellerId { get; set; }
    public SellerProfileDto? Seller { get; set; }

    public decimal? TotalRefundAmount { get; set; }
    public string? RefundReason { get; set; }
}