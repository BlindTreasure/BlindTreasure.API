using BlindTreasure.Application.Interfaces;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlindTreasure.API.Controllers
{
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
        [Authorize(Roles = "Seller")]
        [ProducesResponseType(typeof(SellerDashboardStatisticsDto), 200)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<IActionResult> GetMyStatistics([FromBody] SellerStatisticsRequestDto request, CancellationToken ct)
        {
            try
            {
                var sellerId = _claimsService.CurrentUserId;
                if (sellerId == Guid.Empty)
                    return Forbid("Không tìm thấy seller đang đăng nhập.");

                var seller = await _sellerService.GetSellerProfileByIdAsync(sellerId);
                if (seller == null)
                    return NotFound("Seller không tồn tại.");

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
        /// Lấy thống kê tổng quan cho seller truyền vào (dành cho staff/admin).
        /// </summary>
        /// <param name="sellerId">Id của seller cần thống kê</param>
        /// <param name="request">Tham số thống kê (range, ngày bắt đầu/kết thúc nếu custom)</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns>SellerDashboardStatisticsDto</returns>
        [HttpPost("{sellerId}")]
        [Authorize(Roles = "Admin,Staff")]
        [ProducesResponseType(typeof(SellerDashboardStatisticsDto), 200)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<IActionResult> GetStatisticsBySellerId(Guid sellerId, [FromBody] SellerStatisticsRequestDto request, CancellationToken ct)
        {
            try
            {
                var seller = await _sellerService.GetSellerProfileByIdAsync(sellerId);
                if (seller == null)
                    return NotFound("Seller không tồn tại.");

                var result = await _sellerStatisticsService.GetDashboardStatisticsAsync(sellerId, request, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Có thể log lỗi tại đây nếu cần
                return StatusCode(500, $"Đã xảy ra lỗi: {ex.Message}");
            }
        }
    }
}
