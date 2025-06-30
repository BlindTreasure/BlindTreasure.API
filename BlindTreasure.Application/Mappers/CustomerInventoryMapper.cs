using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Mappers;

public static class CustomerInventoryMapper
{
    public static CustomerInventoryDto ToCustomerInventoryBlindBoxDto(CustomerBlindBox item)
    {
        return new CustomerInventoryDto
        {
            Id = item.Id,
            UserId = item.UserId,
            BlindBoxId = item.BlindBoxId,
            IsOpened = item.IsOpened,
            CreatedAt = item.CreatedAt,
            IsDeleted = item.IsDeleted,
            OpenedAt = item.OpenedAt,
            OrderDetailId = item.OrderDetailId,
            BlindBox = item.BlindBox != null ? BlindBoxDtoMapper.ToBlindBoxDetailDto(item.BlindBox) : null,
            OrderDetail = item.OrderDetail != null ? OrderDtoMapper.ToOrderDetailDto(item.OrderDetail) : null
        };
    }
}