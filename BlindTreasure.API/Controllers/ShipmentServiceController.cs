using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    /// <summary>
    /// API dịch vụ vận chuyển tích hợp GHTK: xác thực, đăng đơn, tra cứu vận đơn.
    /// </summary>
    [ApiController]
    [Route("api/shipment")]
    public class ShipmentServiceController : ControllerBase
    {
        private readonly IGhtkService _ghtkService;

        public ShipmentServiceController(IGhtkService ghtkService)
        {
            _ghtkService = ghtkService;
        }

        /// <summary>
        /// Xác thực token với GHTK API.
        /// </summary>
        /// <returns>Kết quả xác thực từ GHTK.</returns>
        [HttpGet("authenticate")]
        [ProducesResponseType(typeof(ApiResult<GhtkAuthResponse>), 200)]
        [ProducesResponseType(typeof(ApiResult<GhtkAuthResponse>), 400)]
        public async Task<IActionResult> Authenticate()
        {
            try
            {
                var res = await _ghtkService.AuthenticateAsync();
                if (!res.Success.GetValueOrDefault())
                {
                    var code = res.StatusCode ?? "500";
                    return StatusCode(int.TryParse(code, out var statusCode) ? statusCode : 400,
                        ApiResult<GhtkAuthResponse>.Failure(code, res.Message ?? "GHTK authentication failed.", res));
                }
                return Ok(ApiResult<GhtkAuthResponse>.Success(res, "200", "Xác thực GHTK thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResult<GhtkAuthResponse>.Failure("500", ex.Message));
            }
        }

        /// <summary>
        /// Đăng đơn hàng lên GHTK.
        /// </summary>
        /// <param name="req">Thông tin đơn hàng gửi lên GHTK.</param>
        /// <returns>Kết quả đăng đơn hàng từ GHTK.</returns>
        [HttpPost("order")]
        [ProducesResponseType(typeof(ApiResult<GhtkSubmitOrderResponse>), 200)]
        [ProducesResponseType(typeof(ApiResult<GhtkSubmitOrderResponse>), 400)]
        public async Task<IActionResult> SubmitOrder([FromBody] GhtkSubmitOrderRequest req)
        {
            try
            {
                var res = await _ghtkService.SubmitOrderAsync(req);
                if (!res.Success)
                    return StatusCode(int.TryParse(res.StatusCode, out var code) ? code : 400,
                        ApiResult<GhtkSubmitOrderResponse>.Failure(res.StatusCode ?? "400", res.Message, res));
                return Ok(ApiResult<GhtkSubmitOrderResponse>.Success(res, "200", "Đăng đơn hàng GHTK thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResult<GhtkSubmitOrderResponse>.Failure("500", ex.Message));
            }
        }

        /// <summary>
        /// Tra cứu vận đơn GHTK theo mã tracking.
        /// </summary>
        /// <param name="trackingOrder">Mã vận đơn cần tra cứu.</param>
        /// <returns>Kết quả tra cứu vận đơn từ GHTK.</returns>
        [HttpGet("track/{trackingOrder}")]
        [ProducesResponseType(typeof(ApiResult<GhtkTrackResponse>), 200)]
        [ProducesResponseType(typeof(ApiResult<GhtkTrackResponse>), 400)]
        public async Task<IActionResult> TrackOrder(string trackingOrder)
        {
            try
            {
                var res = await _ghtkService.TrackOrderAsync(trackingOrder);
                if (!res.Success)
                    return BadRequest(ApiResult<GhtkTrackResponse>.Failure("400", res.Message, res));
                return Ok(ApiResult<GhtkTrackResponse>.Success(res, "200", "Tra cứu vận đơn thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResult<GhtkTrackResponse>.Failure("500", ex.Message));
            }
        }

        [HttpPost("fee")]
        [ProducesResponseType(typeof(ApiResult<GhtkFeeResponse>), 200)]
        [ProducesResponseType(typeof(ApiResult<GhtkFeeResponse>), 400)]
        public async Task<IActionResult> CalculateFee([FromBody] GhtkFeeRequest req)
        {
            try
            {
                var res = await _ghtkService.CalculateFeeAsync(req);
                if (!res.Success)
                    return BadRequest(ApiResult<GhtkFeeResponse>.Failure(res.StatusCode, res.Message, res));
                return Ok(ApiResult<GhtkFeeResponse>.Success(res, "200", "Tính phí thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResult<GhtkFeeResponse>.Failure("500", ex.Message));
            }
        }
    }
}