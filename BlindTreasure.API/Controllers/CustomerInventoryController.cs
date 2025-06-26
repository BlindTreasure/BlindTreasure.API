using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    /// <summary>
    ///     API quản lý kho BlindBox (CustomerInventory) của người dùng.
    ///     Cho phép xem danh sách, chi tiết, đánh dấu mở box và xóa mềm.
    ///     Không cho phép tạo trực tiếp qua API (chỉ tạo qua luồng thanh toán).
    /// </summary>
    [Route("api/customer-inventories")]
    [ApiController]
    [Authorize]
    public class CustomerInventoryController : ControllerBase
    {
        private readonly ICustomerInventoryService _customerInventoryService;
        private readonly ILoggerService _logger;

        public CustomerInventoryController(ICustomerInventoryService customerInventoryService, ILoggerService logger)
        {
            _customerInventoryService = customerInventoryService;
            _logger = logger;
        }

        /// <summary>
        ///     Lấy toàn bộ BlindBox đã mua của user hiện tại.
        /// </summary>
        /// <returns>Danh sách BlindBox trong kho</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<List<CustomerInventoryDto>>), 200)]
        public async Task<IActionResult> GetMyBlindBoxes()
        {
            try
            {
                var items = await _customerInventoryService.GetByUserIdAsync();
                _logger.Info("[CustomerInventoryController][GetMyBlindBoxes] Lấy danh sách BlindBox thành công.");
                return Ok(ApiResult<List<CustomerInventoryDto>>.Success(items, "200", "Lấy danh sách BlindBox thành công."));
            }
            catch (Exception ex)
            {
                _logger.Error($"[CustomerInventoryController][GetMyBlindBoxes] {ex.Message}");
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var error = ExceptionUtils.CreateErrorResponse<List<CustomerInventoryDto>>(ex);
                return StatusCode(statusCode, error);
            }
        }

        /// <summary>
        ///     Lấy chi tiết một BlindBox trong kho theo Id.
        /// </summary>
        /// <param name="id">Id BlindBox trong kho</param>
        /// <returns>Chi tiết BlindBox</returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<CustomerInventoryDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var item = await _customerInventoryService.GetByIdAsync(id);
                if (item == null)
                {
                    _logger.Warn($"[CustomerInventoryController][GetById] Không tìm thấy BlindBox {id}");
                    return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy BlindBox trong kho."));
                }

                _logger.Info($"[CustomerInventoryController][GetById] Lấy chi tiết BlindBox {id} thành công.");
                return Ok(ApiResult<CustomerInventoryDto>.Success(item, "200", "Lấy chi tiết BlindBox thành công."));
            }
            catch (Exception ex)
            {
                _logger.Error($"[CustomerInventoryController][GetById] {ex.Message}");
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var error = ExceptionUtils.CreateErrorResponse<CustomerInventoryDto>(ex);
                return StatusCode(statusCode, error);
            }
        }

        /// <summary>
        ///     Đánh dấu BlindBox đã mở (IsOpened = true).
        /// </summary>
        /// <param name="id">Id BlindBox trong kho</param>
        /// <returns>BlindBox đã cập nhật trạng thái mở</returns>
        [HttpPut("{id:guid}/open")]
        [ProducesResponseType(typeof(ApiResult<CustomerInventoryDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> MarkAsOpened(Guid id)
        {
            try
            {
                var result = await _customerInventoryService.MarkAsOpenedAsync(id);
                _logger.Success($"[CustomerInventoryController][MarkAsOpened] Đánh dấu BlindBox {id} đã mở.");
                return Ok(ApiResult<CustomerInventoryDto>.Success(result, "200", "Đánh dấu BlindBox đã mở thành công."));
            }
            catch (Exception ex)
            {
                _logger.Error($"[CustomerInventoryController][MarkAsOpened] {ex.Message}");
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var error = ExceptionUtils.CreateErrorResponse<CustomerInventoryDto>(ex);
                return StatusCode(statusCode, error);
            }
        }

        /// <summary>
        ///     Xóa mềm một BlindBox khỏi kho của user.
        /// </summary>
        /// <param name="id">Id BlindBox trong kho</param>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _customerInventoryService.DeleteAsync(id);
                if (!success)
                {
                    _logger.Warn($"[CustomerInventoryController][Delete] Không tìm thấy BlindBox {id}");
                    return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy BlindBox trong kho."));
                }

                _logger.Success($"[CustomerInventoryController][Delete] Xóa BlindBox {id} thành công.");
                return Ok(ApiResult<object>.Success(null, "200", "Xóa BlindBox thành công."));
            }
            catch (Exception ex)
            {
                _logger.Error($"[CustomerInventoryController][Delete] {ex.Message}");
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var error = ExceptionUtils.CreateErrorResponse<object>(ex);
                return StatusCode(statusCode, error);
            }
        }
    }
}
