using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlindTreasure.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SellerStatisticsController : ControllerBase
{
    private readonly ISellerStatisticsService _sellerStatisticsService;
    private readonly IClaimsService _claimsService;
    private readonly ISellerService _sellerService;

    public SellerStatisticsController(
        ISellerStatisticsService sellerStatisticsService,
        IClaimsService claimsService,
        ISellerService sellerService)
    {
        _sellerStatisticsService = sellerStatisticsService;
        _claimsService = claimsService;
        _sellerService = sellerService;
    }

    /// <summary>
    /// Lấy thống kê tổng quan cho seller đang đăng nhập (dashboard seller).
    /// </summary>
    /// <param name="request">Tham số thống kê (range, ngày bắt đầu/kết thúc nếu custom)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>SellerDashboardStatisticsDto</returns>
    [HttpPost("me")]
    //[Authorize(Roles = "Seller")]
    [ProducesResponseType(typeof(SellerDashboardStatisticsDto), 200)]
    [ProducesResponseType(typeof(string), 403)]
    [ProducesResponseType(typeof(string), 404)]
    public async Task<IActionResult> GetMyStatistics([FromBody] SellerStatisticsRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var sellerId = _claimsService.CurrentUserId;
            if (sellerId == Guid.Empty)
                return Forbid("Không tìm thấy người bán đang đăng nhập.");

            var seller = await _sellerService.GetSellerProfileByUserIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");

            var result = await _sellerStatisticsService.GetDashboardStatisticsAsync(seller.SellerId, request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            // Có thể log lỗi tại đây nếu cần
            return StatusCode(500, $"Đã xảy ra lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Lấy thống kê tổng quan cho seller truyền vào (dành cho staff/admin).
    /// </summary>
    /// <param name="sellerId">Id của seller cần thống kê</param>
    /// <param name="request">Tham số thống kê (range, ngày bắt đầu/kết thúc nếu custom)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>SellerDashboardStatisticsDto</returns>
    [HttpPost("{sellerId}")]
    //    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(SellerDashboardStatisticsDto), 200)]
    [ProducesResponseType(typeof(string), 404)]
    public async Task<IActionResult> GetStatisticsBySellerId(Guid sellerId,
        [FromBody] SellerStatisticsRequestDto request, CancellationToken ct)
    {
        try
        {
            var seller = await _sellerService.GetSellerProfileByIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");

            var result = await _sellerStatisticsService.GetDashboardStatisticsAsync(sellerId, request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            // Có thể log lỗi tại đây nếu cần
            return StatusCode(500, $"Đã xảy ra lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Lấy thống kê tổng quan cho seller (Overview).
    /// </summary>
    [Authorize(Roles = "Seller,Admin,Staff")]
    [HttpPost("overview")]
    [ProducesResponseType(typeof(ApiResult<SellerOverviewStatisticsDto>), 200)]
    public async Task<IActionResult> GetOverviewStatistics([FromBody] SellerStatisticsRequestDto req,
        CancellationToken ct)
    {
        try
        {
            var sellerId = _claimsService.CurrentUserId;
            if (sellerId == Guid.Empty)
                return Forbid("Không tìm thấy người bán đang đăng nhập.");

            var seller = await _sellerService.GetSellerProfileByUserIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");
            var result = await _sellerStatisticsService.GetOverviewStatisticsAsync(seller.SellerId, req, ct);
            return Ok(ApiResult<SellerOverviewStatisticsDto>.Success(result, "200", "Thống kê tổng quan thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<SellerOverviewStatisticsDto>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    /// Lấy top 5 sản phẩm bán chạy nhất.
    /// </summary>
    [Authorize(Roles = "Seller,Admin,Staff")]
    [HttpPost("top-products")]
    [ProducesResponseType(typeof(ApiResult<List<TopSellingProductDto>>), 200)]
    public async Task<IActionResult> GetTopProducts([FromBody] SellerStatisticsRequestDto req, CancellationToken ct)
    {
        try
        {
            var sellerId = _claimsService.CurrentUserId;
            if (sellerId == Guid.Empty)
                return Forbid("Không tìm thấy người bán đang đăng nhập.");

            var seller = await _sellerService.GetSellerProfileByUserIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");
            var result = await _sellerStatisticsService.GetTopProductsAsync(seller.SellerId, req, ct);
            return Ok(ApiResult<List<TopSellingProductDto>>.Success(result, "200", "Lấy top sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<List<TopSellingProductDto>>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    /// Lấy top 5 blindbox bán chạy nhất.
    /// </summary>
    [Authorize(Roles = "Seller,Admin,Staff")]
    [HttpPost("top-blindboxes")]
    [ProducesResponseType(typeof(ApiResult<List<TopSellingBlindBoxDto>>), 200)]
    public async Task<IActionResult> GetTopBlindBoxes([FromBody] SellerStatisticsRequestDto req, CancellationToken ct)
    {
        try
        {
            var sellerId = _claimsService.CurrentUserId;
            if (sellerId == Guid.Empty)
                return Forbid("Không tìm thấy người bán đang đăng nhập.");

            var seller = await _sellerService.GetSellerProfileByUserIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");
            var result = await _sellerStatisticsService.GetTopBlindBoxesAsync(seller.SellerId, req, ct);
            return Ok(ApiResult<List<TopSellingBlindBoxDto>>.Success(result, "200", "Lấy top blindbox thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<List<TopSellingBlindBoxDto>>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    /// Lấy thống kê trạng thái đơn hàng.
    /// </summary>
    [Authorize(Roles = "Seller,Admin,Staff")]
    [HttpPost("order-status")]
    [ProducesResponseType(typeof(ApiResult<List<OrderStatusStatisticsDto>>), 200)]
    public async Task<IActionResult> GetOrderStatusStatistics([FromBody] SellerStatisticsRequestDto req,
        CancellationToken ct)
    {
        try
        {
            var sellerId = _claimsService.CurrentUserId;
            if (sellerId == Guid.Empty)
                return Forbid("Không tìm thấy người bán đang đăng nhập.");

            var seller = await _sellerService.GetSellerProfileByUserIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");
            var result = await _sellerStatisticsService.GetOrderStatusStatisticsAsync(seller.SellerId, req, ct);
            return Ok(ApiResult<List<OrderStatusStatisticsDto>>.Success(result, "200",
                "Lấy thống kê trạng thái đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<List<OrderStatusStatisticsDto>>.Failure("500", ex.Message));
        }
    }

    /// <summary>
    /// Lấy thống kê theo thời gian (time series).
    /// </summary>
    [Authorize(Roles = "Seller,Admin,Staff")]
    [HttpPost("time-series")]
    [ProducesResponseType(typeof(ApiResult<SellerStatisticsResponseDto>), 200)]
    public async Task<IActionResult> GetTimeSeriesStatistics([FromBody] SellerStatisticsRequestDto req,
        CancellationToken ct)
    {
        try
        {
            var sellerId = _claimsService.CurrentUserId;
            if (sellerId == Guid.Empty)
                return Forbid("Không tìm thấy người bán đang đăng nhập.");

            var seller = await _sellerService.GetSellerProfileByUserIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");
            var result = await _sellerStatisticsService.GetTimeSeriesStatisticsAsync(seller.SellerId, req, ct);
            return Ok(ApiResult<SellerStatisticsResponseDto>.Success(result, "200",
                "Lấy thống kê theo thời gian thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<SellerStatisticsResponseDto>.Failure("500", ex.Message));
        }
    }


    [Authorize(Roles = "Seller,Admin,Staff")]
    [HttpPost("revenue-summary")]
    [ProducesResponseType(typeof(ApiResult<SellerRevenueSummaryDto>), 200)]
    public async Task<IActionResult> GetRevenueSummary([FromBody] SellerStatisticsRequestDto req, CancellationToken ct)
    {
        try
        {
            var sellerId = _claimsService.CurrentUserId;
            if (sellerId == Guid.Empty)
                return Forbid("Không tìm thấy người bán đang đăng nhập.");

            var seller = await _sellerService.GetSellerProfileByUserIdAsync(sellerId);
            if (seller == null)
                return NotFound("Người bán không tồn tại.");

            var result = await _sellerStatisticsService.GetRevenueSummaryAsync(seller.SellerId, req, ct);
            return Ok(ApiResult<SellerRevenueSummaryDto>.Success(result, "200", "Lấy doanh thu thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<SellerRevenueSummaryDto>.Failure("500", ex.Message));
        }
    }
}