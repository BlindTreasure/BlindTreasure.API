using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AdminStatisticDTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace BlindTreasure.API.Controllers
{
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
        /// Lấy thống kê dashboard cho admin (doanh thu, đơn hàng, seller, khách hàng, top category, time series).
        /// </summary>
        [HttpPost("stats")]
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
                // Nếu có logger, có thể log lỗi tại đây
                return StatusCode(500, ApiResult<object>.Failure("500", "Có lỗi xảy ra khi lấy thống kê dashboard: " + ex.Message));
            }
        }
    }
}
