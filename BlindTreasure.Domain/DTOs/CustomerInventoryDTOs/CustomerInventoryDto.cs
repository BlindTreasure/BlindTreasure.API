﻿using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;

namespace BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;

public class CustomerInventoryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BlindBoxId { get; set; }
    public BlindBoxDetailDto? BlindBox { get; set; }


    public bool IsOpened { get; set; } = false;
    public DateTime? OpenedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid? OrderDetailId { get; set; }
    public OrderDetailDto? OrderDetail { get; set; }
}