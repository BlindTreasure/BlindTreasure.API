﻿using BlindTreasure.Domain.DTOs.PaymentDTOs;

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
    public Guid? PromotionId { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? PromotionNote { get; set; }
}