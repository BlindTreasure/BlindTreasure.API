﻿namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class CreateCheckoutRequestDto
{
    public Guid? ShippingAddressId { get; set; }

    public Guid? PromotionId { get; set; }
    // Có thể bổ sung thêm payment info, note, v.v.
}