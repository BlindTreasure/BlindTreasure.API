﻿using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class PromotionQueryParameter : PaginationParameter
{
    /// <summary>
    ///     Lọc theo trạng thái: PENDING, Approved, Rejected
    /// </summary>
    public PromotionStatus? Status { get; set; }

    public Guid? SellerId { get; set; } // Cho phép lọc theo seller cụ thể
}