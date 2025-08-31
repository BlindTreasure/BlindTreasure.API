using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface ISellerStatisticsService
{
    Task<SellerDashboardStatisticsDto> GetDashboardStatisticsAsync(Guid sellerId, SellerStatisticsRequestDto req,
        CancellationToken ct = default);

    Task<List<OrderStatusStatisticsDto>> GetOrderStatusStatisticsAsync(Guid sellerId, SellerStatisticsRequestDto req,
        CancellationToken ct = default);

    Task<SellerOverviewStatisticsDto> GetOverviewStatisticsAsync(Guid sellerId, SellerStatisticsRequestDto req,
        CancellationToken ct = default);

    Task<SellerRevenueSummaryDto> GetRevenueSummaryAsync(Guid sellerId, SellerStatisticsRequestDto req,
        CancellationToken ct = default);

    Task<SellerStatisticsResponseDto> GetTimeSeriesStatisticsAsync(Guid sellerId, SellerStatisticsRequestDto req,
        CancellationToken ct = default);

    Task<List<TopSellingBlindBoxDto>> GetTopBlindBoxesAsync(Guid sellerId, SellerStatisticsRequestDto req,
        CancellationToken ct = default);

    Task<List<TopSellingProductDto>> GetTopProductsAsync(Guid sellerId, SellerStatisticsRequestDto req,
        CancellationToken ct = default);
}