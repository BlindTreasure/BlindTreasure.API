﻿namespace BlindTreasure.Domain.DTOs.Pagination;

public class PaginationParameter
{
    private const int maxPageSize = 50;
    private int _pageSize = 5; // DEPENDENCE ON PROJECT
    public int PageIndex { get; set; } = 0;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > maxPageSize ? maxPageSize : value;
    }
}