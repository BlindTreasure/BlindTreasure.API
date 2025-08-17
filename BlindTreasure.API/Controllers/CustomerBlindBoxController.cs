using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/customer-blindboxes")]
[ApiController]
[Authorize]
public class CustomerBlindBoxController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly ICustomerBlindBoxService _customerBlindBoxService;
    private readonly ILoggerService _logger;

    public CustomerBlindBoxController(ICustomerBlindBoxService customerBlindBoxService, ILoggerService logger,
        IClaimsService claimsService)
    {
        _customerBlindBoxService = customerBlindBoxService;
        _logger = logger;
        _claimsService = claimsService;
    }

    /// <summary>
    ///     Lấy toàn bộ BlindBox đã mua của user hiện tại.
    /// </summary>
    /// <returns>Danh sách BlindBox trong kho</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<CustomerInventoryDto>>), 200)]
    public async Task<IActionResult> GetMyBlindBoxes([FromQuery] CustomerBlindBoxQueryParameter param)
    {
        try
        {
            var result = await _customerBlindBoxService.GetMyBlindBoxesAsync(param);
            _logger.Info("[CustomerBlindBoxController][GetMyBlindBoxes] Lấy danh sách BlindBox thành công.");
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Danh sách Blind Box trong kho của bạn đã được tải thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[CustomerBlindBoxController][GetMyBlindBoxes] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
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
            var item = await _customerBlindBoxService.GetByIdAsync(id);
            if (item == null)
            {
                _logger.Warn($"[CustomerInventoryController][GetById] Không tìm thấy BlindBox {id}");
                return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy Blind Box trong kho của bạn."));
            }

            _logger.Info($"[CustomerInventoryController][GetById] Lấy chi tiết BlindBox {id} thành công.");
            return Ok(ApiResult<CustomerInventoryDto>.Success(item, "200", "Thông tin chi tiết Blind Box đã được tải thành công."));
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
            var result = await _customerBlindBoxService.MarkAsOpenedAsync(id);
            _logger.Success($"[CustomerInventoryController][MarkAsOpened] Đánh dấu BlindBox {id} đã mở.");
            return Ok(ApiResult<CustomerInventoryDto>.Success(result, "200", "Blind Box đã được đánh dấu là đã mở thành công."));
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
            var success = await _customerBlindBoxService.DeleteAsync(id);
            if (!success)
            {
                _logger.Warn($"[CustomerInventoryController][Delete] Không tìm thấy BlindBox {id}");
                return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy Blind Box trong kho để xóa."));
            }

            _logger.Success($"[CustomerInventoryController][Delete] Xóa BlindBox {id} thành công.");
            return Ok(ApiResult<object>.Success(null, "200", "Blind Box đã được xóa khỏi kho của bạn thành công."));
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