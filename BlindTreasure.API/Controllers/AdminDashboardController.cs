using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AdminStatisticDTOs;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlindTreasure.API.Controllers;

[Route("api/admin-dashboard")]
[ApiController]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _adminDashboardService;

    public AdminDashboardController(IAdminDashboardService adminDashboardService)
    {
        _adminDashboardService = adminDashboardService;
    }

    /// <summary>
    /// Get full dashboard statistics for admin (revenue, orders, sellers, customers, top categories, time series).
    /// </summary>
    /// <remarks>
    /// <b>AdminDashboardRange options:</b>
    /// <list type="bullet">
    ///   <item><b>Today</b> (1): Statistics for today (UTC).</item>
    ///   <item><b>Week</b> (2): Statistics for the current week (starting Monday, UTC).</item>
    ///   <item><b>Month</b> (3): Statistics for the current month (UTC).</item>
    ///   <item><b>Quarter</b> (4): Statistics for the current quarter (UTC).</item>
    ///   <item><b>Year</b> (5): Statistics for the current year (UTC).</item>
    ///   <item><b>Custom</b> (6): Statistics for a custom date range (provide StartDate and EndDate).</item>
    /// </list>
    /// </remarks>
    [HttpPost("full-stats")]
    [ProducesResponseType(typeof(ApiResult<AdminDashBoardDtos>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetDashboardStats([FromBody] AdminDashboardRequestDto req)
    {
        try
        {
            var result = await _adminDashboardService.GetDashboardAsync(req);
            return Ok(ApiResult<AdminDashBoardDtos>.Success(result, "200", "Lấy thống kê dashboard thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500,
                ApiResult<object>.Failure("500", "Có lỗi xảy ra khi lấy thống kê dashboard: " + ex.Message));
        }
    }

    /// <summary>
    /// Get revenue summary for admin dashboard.
    /// </summary>
    /// <remarks>
    /// <b>AdminDashboardRange options:</b>
    /// <list type="bullet">
    ///   <item><b>Today</b> (1): Statistics for today (UTC).</item>
    ///   <item><b>Week</b> (2): Statistics for the current week (starting Monday, UTC).</item>
    ///   <item><b>Month</b> (3): Statistics for the current month (UTC).</item>
    ///   <item><b>Quarter</b> (4): Statistics for the current quarter (UTC).</item>
    ///   <item><b>Year</b> (5): Statistics for the current year (UTC).</item>
    ///   <item><b>Custom</b> (6): Statistics for a custom date range (provide StartDate and EndDate).</item>
    /// </list>
    /// </remarks>
    [HttpPost("revenue-summary")]
    [ProducesResponseType(typeof(ApiResult<RevenueSummaryDto>), 200)]
    public async Task<IActionResult> GetRevenueSummary([FromBody] AdminDashboardRequestDto req)
    {
        var result = await _adminDashboardService.GetRevenueSummaryAsync(req);
        return Ok(ApiResult<RevenueSummaryDto>.Success(result, "200", "Lấy doanh thu thành công."));
    }

    /// <summary>
    /// Get order summary for admin dashboard.
    /// </summary>
    /// <remarks>
    /// <b>AdminDashboardRange options:</b>
    /// <list type="bullet">
    ///   <item><b>Today</b> (1): Statistics for today (UTC).</item>
    ///   <item><b>Week</b> (2): Statistics for the current week (starting Monday, UTC).</item>
    ///   <item><b>Month</b> (3): Statistics for the current month (UTC).</item>
    ///   <item><b>Quarter</b> (4): Statistics for the current quarter (UTC).</item>
    ///   <item><b>Year</b> (5): Statistics for the current year (UTC).</item>
    ///   <item><b>Custom</b> (6): Statistics for a custom date range (provide StartDate and EndDate).</item>
    /// </list>
    /// </remarks>
    [HttpPost("order-summary")]
    [ProducesResponseType(typeof(ApiResult<OrderSummaryDto>), 200)]
    public async Task<IActionResult> GetOrderSummary([FromBody] AdminDashboardRequestDto req)
    {
        var result = await _adminDashboardService.GetOrderSummaryAsync(req);
        return Ok(ApiResult<OrderSummaryDto>.Success(result, "200", "Lấy thống kê đơn hàng thành công."));
    }

    /// <summary>
    /// Get seller summary for admin dashboard.
    /// </summary>
    /// <remarks>
    /// <b>AdminDashboardRange options:</b>
    /// <list type="bullet">
    ///   <item><b>Today</b> (1): Statistics for today (UTC).</item>
    ///   <item><b>Week</b> (2): Statistics for the current week (starting Monday, UTC).</item>
    ///   <item><b>Month</b> (3): Statistics for the current month (UTC).</item>
    ///   <item><b>Quarter</b> (4): Statistics for the current quarter (UTC).</item>
    ///   <item><b>Year</b> (5): Statistics for the current year (UTC).</item>
    ///   <item><b>Custom</b> (6): Statistics for a custom date range (provide StartDate and EndDate).</item>
    /// </list>
    /// </remarks>
    [HttpPost("seller-summary")]
    [ProducesResponseType(typeof(ApiResult<SellerSummaryDto>), 200)]
    public async Task<IActionResult> GetSellerSummary([FromBody] AdminDashboardRequestDto req)
    {
        var result = await _adminDashboardService.GetSellerSummaryAsync(req);
        return Ok(ApiResult<SellerSummaryDto>.Success(result, "200", "Lấy thống kê seller thành công."));
    }

    /// <summary>
    /// Get customer summary for admin dashboard.
    /// </summary>
    /// <remarks>
    /// <b>AdminDashboardRange options:</b>
    /// <list type="bullet">
    ///   <item><b>Today</b> (1): Statistics for today (UTC).</item>
    ///   <item><b>Week</b> (2): Statistics for the current week (starting Monday, UTC).</item>
    ///   <item><b>Month</b> (3): Statistics for the current month (UTC).</item>
    ///   <item><b>Quarter</b> (4): Statistics for the current quarter (UTC).</item>
    ///   <item><b>Year</b> (5): Statistics for the current year (UTC).</item>
    ///   <item><b>Custom</b> (6): Statistics for a custom date range (provide StartDate and EndDate).</item>
    /// </list>
    /// </remarks>
    [HttpPost("customer-summary")]
    [ProducesResponseType(typeof(ApiResult<CustomerSummaryDto>), 200)]
    public async Task<IActionResult> GetCustomerSummary([FromBody] AdminDashboardRequestDto req)
    {
        var result = await _adminDashboardService.GetCustomerSummaryAsync(req);
        return Ok(ApiResult<CustomerSummaryDto>.Success(result, "200", "Lấy thống kê khách hàng thành công."));
    }

    /// <summary>
    /// Get top categories for admin dashboard.
    /// </summary>
    /// <remarks>
    /// <b>AdminDashboardRange options:</b>
    /// <list type="bullet">
    ///   <item><b>Today</b> (1): Statistics for today (UTC).</item>
    ///   <item><b>Week</b> (2): Statistics for the current week (starting Monday, UTC).</item>
    ///   <item><b>Month</b> (3): Statistics for the current month (UTC).</item>
    ///   <item><b>Quarter</b> (4): Statistics for the current quarter (UTC).</item>
    ///   <item><b>Year</b> (5): Statistics for the current year (UTC).</item>
    ///   <item><b>Custom</b> (6): Statistics for a custom date range (provide StartDate and EndDate).</item>
    /// </list>
    /// </remarks>
    [HttpPost("top-categories")]
    [ProducesResponseType(typeof(ApiResult<List<CategoryRevenueDto>>), 200)]
    public async Task<IActionResult> GetTopCategories([FromBody] AdminDashboardRequestDto req)
    {
        var result = await _adminDashboardService.GetTopCategoriesAsync(req);
        return Ok(ApiResult<List<CategoryRevenueDto>>.Success(result, "200", "Lấy top category thành công."));
    }

    /// <summary>
    /// Get time series data for admin dashboard charts.
    /// </summary>
    /// <remarks>
    /// <b>AdminDashboardRange options:</b>
    /// <list type="bullet">
    ///   <item><b>Today</b> (1): Statistics for today (UTC).</item>
    ///   <item><b>Week</b> (2): Statistics for the current week (starting Monday, UTC).</item>
    ///   <item><b>Month</b> (3): Statistics for the current month (UTC).</item>
    ///   <item><b>Quarter</b> (4): Statistics for the current quarter (UTC).</item>
    ///   <item><b>Year</b> (5): Statistics for the current year (UTC).</item>
    ///   <item><b>Custom</b> (6): Statistics for a custom date range (provide StartDate and EndDate).</item>
    /// </list>
    /// </remarks>
    [HttpPost("time-series")]
    [ProducesResponseType(typeof(ApiResult<TimeSeriesDto>), 200)]
    public async Task<IActionResult> GetTimeSeries([FromBody] AdminDashboardRequestDto req)
    {
        var result = await _adminDashboardService.GetTimeSeriesAsync(req);
        return Ok(ApiResult<TimeSeriesDto>.Success(result, "200", "Lấy time series thành công."));
    }
}