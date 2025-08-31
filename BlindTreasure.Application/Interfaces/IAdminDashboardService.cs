using BlindTreasure.Domain.DTOs.AdminStatisticDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashBoardDtos> GetDashboardAsync(AdminDashboardRequestDto req);
    Task<RevenueSummaryDto> GetRevenueSummaryAsync(AdminDashboardRequestDto req);
    Task<OrderSummaryDto> GetOrderSummaryAsync(AdminDashboardRequestDto req);
    Task<SellerSummaryDto> GetSellerSummaryAsync(AdminDashboardRequestDto req);
    Task<CustomerSummaryDto> GetCustomerSummaryAsync(AdminDashboardRequestDto req);
    Task<List<CategoryRevenueDto>> GetTopCategoriesAsync(AdminDashboardRequestDto req);
    Task<TimeSeriesDto> GetTimeSeriesAsync(AdminDashboardRequestDto req);
}