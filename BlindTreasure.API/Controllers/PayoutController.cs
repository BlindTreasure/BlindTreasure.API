using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PayoutDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    [Route("api/payouts")]
    [ApiController]
    public class PayoutController : ControllerBase
    {
        private readonly IPayoutService _payoutService;
        private readonly ILoggerService _loggerService;
        private readonly IClaimsService _claimsService;
        private readonly ISellerService _sellerService;

        public PayoutController(IPayoutService payoutService, ILoggerService loggerService, IClaimsService claimsService, ISellerService sellerService)
        {
            _payoutService = payoutService;
            _loggerService = loggerService;
            _claimsService = claimsService;
            _sellerService = sellerService;
        }

        /// <summary>
        /// Seller gửi yêu cầu rút tiền cho payout đang chờ (PENDING).
        /// Chuyển trạng thái payout sang REQUESTED, staff sẽ duyệt.
        /// </summary>
        [HttpPost("request")]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        [Authorize]
        public async Task<IActionResult> RequestPayout()
        {
            try
            {
                var userId = _claimsService.CurrentUserId;
                var seller = await _sellerService.GetSellerProfileByUserIdAsync(userId);
                if (seller == null)
                    return BadRequest(ApiResult<object>.Failure("400", "Không tìm thấy hồ sơ seller."));

                var success = await _payoutService.RequestPayoutAsync(seller.SellerId);
                if (success == null )
                    return BadRequest(ApiResult<object>.Failure("400", "Không có payout hợp lệ hoặc doanh thu thực chưa đủ để rút."));

                return Ok(ApiResult<object>.Success(success, "200", "Yêu cầu rút tiền đã được gửi thành công. Chờ staff duyệt."));
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[RequestPayout] {ex.Message}");
                return StatusCode(500, ApiResult<bool>.Failure("500", "Có lỗi xảy ra khi gửi yêu cầu rút tiền" + ex.Message));
            }
        }

        /// <summary>
        /// Seller kiểm tra payout hiện tại đang trong quá trình duyệt và xử lý
        /// </summary>
        [HttpGet("eligible")]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [Authorize]
        public async Task<IActionResult> GetEligiblePayout()
        {
            try
            {
                var userId = _claimsService.CurrentUserId;
                var seller = await _sellerService.GetSellerProfileByUserIdAsync(userId);
                if (seller == null)
                    return BadRequest(ApiResult<object>.Failure("400", "Không tìm thấy hồ sơ seller."));

                var payout = await _payoutService.GetEligiblePayoutForSellerAsync(seller.SellerId);
                if (payout == null)
                    return Ok(ApiResult<object>.Success(null, "200", "Không có payout hợp lệ hoặc chưa đủ điều kiện rút."));

                return Ok(ApiResult<object>.Success(payout, "200", "Payout hợp lệ để rút tiền."));
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[GetEligiblePayout] {ex.Message}");
                return StatusCode(500, ApiResult<object>.Failure("500", "Có lỗi xảy ra khi kiểm tra payout :"+ex.Message));
            }
        }

        /// <summary>
        /// Seller tiến hành rút tiền (Stripe payout). Chỉ gọi khi đã được staff duyệt.
        /// </summary>
        [HttpPost("{sellerId}/process")]
        [ProducesResponseType(typeof(ApiResult<bool>), 200)]
        [ProducesResponseType(typeof(ApiResult<bool>), 400)]
        [Authorize]
        public async Task<IActionResult> ProcessSellerPayout(Guid sellerId)
        {
            try
            {
                //var userId = _claimsService.CurrentUserId;
                var seller = await _sellerService.GetSellerProfileByIdAsync(sellerId);
                if (seller == null)
                    return BadRequest(ApiResult<bool>.Failure("400", "Không tìm thấy hồ sơ seller."));

                var success = await _payoutService.ProcessSellerPayoutAsync(seller.SellerId);
                if (!success)
                    return BadRequest(ApiResult<bool>.Failure("400", "Không thể thực hiện rút tiền. Kiểm tra lại điều kiện hoặc liên hệ hỗ trợ."));

                return Ok(ApiResult<bool>.Success(true, "200", "Yêu cầu rút tiền đã được xử lý thành công."));
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[ProcessSellerPayout] {ex.Message}");
                return StatusCode(500, ApiResult<bool>.Failure("500", "Có lỗi xảy ra khi xử lý rút tiền:." + ex.Message));
            }
        }

        /// <summary>
        /// Seller xem thông tin tính toán payout sắp tới (dự kiến).
        /// </summary>
        [HttpPost("calculate-upcoming")]
        [ProducesResponseType(typeof(ApiResult<PayoutCalculationResultDto>), 200)]
        [Authorize]
        public async Task<IActionResult> CalculateUpcomingPayout()
        {
            try
            {
                var result = await _payoutService.GetUpcomingPayoutForCurrentSellerAsync();
                return Ok(ApiResult<PayoutCalculationResultDto>.Success(result, "200", "Tính toán payout sắp tới thành công."));
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[CalculateUpcomingPayout] {ex.Message}");
                return StatusCode(500, ApiResult<PayoutCalculationResultDto>.Failure("500", "Có lỗi xảy ra khi tính toán payout." + ex.Message));
            }
        }

        /// <summary>
        /// Seller xem lịch sử payout trong một khoảng thời gian.
        /// </summary>
        [HttpPost("history")]
        [ProducesResponseType(typeof(ApiResult<List<PayoutListResponseDto>>), 200)]
        [Authorize]
        public async Task<IActionResult> GetSellerPayoutsForPeriod([FromBody] PayoutCalculationRequestDto req)
        {
            try
            {
                var result = await _payoutService.GetSellerPayoutsForPeriodAsync(req);
                return Ok(ApiResult<List<PayoutListResponseDto>>.Success(result, "200", "Lấy lịch sử payout thành công."));
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[GetSellerPayoutsForPeriod] {ex.Message}");
                return StatusCode(500, ApiResult<List<PayoutListResponseDto>>.Failure("500", "Có lỗi xảy ra khi lấy lịch sử payout." + ex.Message));
            }
        }

        /// <summary>
        /// Seller xem chi tiết một payout theo Id.
        /// </summary>
        [HttpGet("{payoutId}")]
        [ProducesResponseType(typeof(ApiResult<PayoutDetailResponseDto>), 200)]
        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> GetPayoutDetailById(Guid payoutId)
        {
            try
            {
                var result = await _payoutService.GetPayoutDetailByIdAsync(payoutId);
                if (result == null)
                    return NotFound(ApiResult<PayoutDetailResponseDto>.Failure("404", "Không tìm thấy payout."));

                return Ok(ApiResult<PayoutDetailResponseDto>.Success(result, "200", "Lấy chi tiết payout thành công."));
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[GetPayoutDetailById] {ex.Message}");
                return StatusCode(500, ApiResult<PayoutDetailResponseDto>.Failure("500", "Có lỗi xảy ra khi lấy chi tiết payout." + ex.Message));
            }
        }

        [HttpGet("export-latest")]
        [Authorize]
        public async Task<IActionResult> ExportLatestPayoutProof()
        {
            try
            {
                var stream = await _payoutService.ExportLatestPayoutProofAsync();
                if (stream.CanSeek) stream.Position = 0;

                string fileName = $"PayoutProof_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string fileType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return File(stream, fileType, fileName);
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[ExportLatestPayoutProof] {ex.Message}");
                return StatusCode(500, ApiResult<object>.Failure("500", "Có lỗi xảy ra khi export file payout." + ex.Message));
            }
        }

        [HttpPost("{payoutId}/export-history")]
        [Authorize]
        public async Task<IActionResult> ExportPayoutsByPeriod(Guid payoutId)
        {
            try
            {
                var stream = await _payoutService.ExportPayoutByIdAsync(payoutId);
                if (stream.CanSeek) stream.Position = 0;

                string fileName = $"PayoutHistory_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string fileType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return File(stream, fileType, fileName);
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[ExportPayoutsByPeriod] {ex.Message}");
                return StatusCode(500, ApiResult<object>.Failure("500", "Có lỗi xảy ra khi export file payout history." + ex.Message));
            }
        }

        /// <summary>
        /// LIST PAYOUTS CỦA SELLER, KHÔNG CẦN TRUYỀN SELLERID
        /// </summary>
        [HttpGet("my-payouts")]
        [Authorize]
        public async Task<IActionResult> GetMyPayouts([FromQuery] PayoutAdminQueryParameter param)
        {
            try
            {
                var result = await _payoutService.GetPayoutsForCurrentSellerAsync(param);
                return Ok(ApiResult<object>.Success(new
                {
                    result,
                    count = result.Count,
                    pageSize = param.PageSize,
                    currentPage = param.PageIndex,
                    totalPages = (int)Math.Ceiling((double)result.Count / param.PageSize)
                }, "200", "Lấy danh sách payouts của seller thành công."));
            }
            catch (Exception ex)
            {
                _loggerService.Error($"[GetMyPayouts] {ex.Message}");
                return StatusCode(500, ApiResult<object>.Failure("500", "Có lỗi xảy ra khi lấy danh sách payouts của seller." + ex.Message));
            }
        }
    }
}
