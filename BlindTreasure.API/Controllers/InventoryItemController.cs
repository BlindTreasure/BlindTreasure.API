using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

/// <summary>
///     API quản lý kho hàng (Inventory) của người dùng.
///     Cho phép xem danh sách, chi tiết, cập nhật và xóa inventory item.
///     Không cho phép tạo trực tiếp qua API (inventory chỉ được tạo qua luồng mua hàng/thanh toán).
/// </summary>
[Route("api/inventory-items")]
[ApiController]
[Authorize]
public class InventoryItemController : ControllerBase
{
    private readonly IInventoryItemService _inventoryItemService;
    private readonly ILoggerService _logger;

    public InventoryItemController(IInventoryItemService inventoryItemService, ILoggerService logger)
    {
        _inventoryItemService = inventoryItemService;
        _logger = logger;
    }

    /// <summary>
    ///     Lấy toàn bộ inventory item của user hiện tại.
    /// </summary>
    /// <returns>Danh sách inventory item</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<InventoryItemDto>>), 200)]
    public async Task<IActionResult> GetMyInventory([FromQuery] InventoryItemQueryParameter param)
    {
        try
        {
            var result = await _inventoryItemService.GetMyInventoryAsync(param);
            _logger.Info("[InventoryItemController][GetMyInventory] Lấy danh sách inventory thành công.");
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách inventory thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][GetMyInventory] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    [HttpGet("by-blindbox/{blindBoxId}")]
    public async Task<IActionResult> GetUnboxedItemsByBlindBox(Guid blindBoxId)
    {
        try
        {
            var result = await _inventoryItemService.GetMyUnboxedItemsFromBlindBoxAsync(blindBoxId);
            return Ok(ApiResult<List<InventoryItemDto>>.Success(result, "200",
                "Lấy sản phẩm đã mở từ blind box thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<InventoryItemDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Lấy chi tiết một inventory item theo Id.
    /// </summary>
    /// <param name="id">Id inventory item</param>
    /// <returns>Chi tiết inventory item</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<InventoryItemDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var item = await _inventoryItemService.GetByIdAsync(id);
            if (item == null)
            {
                _logger.Warn($"[InventoryItemController][GetById] Không tìm thấy inventory item {id}");
                return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy inventory item."));
            }

            _logger.Info($"[InventoryItemController][GetById] Lấy chi tiết inventory item {id} thành công.");
            return Ok(ApiResult<InventoryItemDto>.Success(item, "200", "Lấy chi tiết inventory item thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][GetById] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<InventoryItemDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Cập nhật thông tin một inventory item.
    /// </summary>
    /// <param name="id">Id inventory item</param>
    /// <param name="dto">Thông tin cập nhật</param>
    /// <returns>Inventory item đã cập nhật</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<InventoryItemDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInventoryItemDto dto)
    {
        try
        {
            var result = await _inventoryItemService.UpdateAsync(id, dto);
            _logger.Success($"[InventoryItemController][Update] Cập nhật inventory item {id} thành công.");
            return Ok(ApiResult<InventoryItemDto>.Success(result, "200", "Cập nhật inventory item thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][Update] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<InventoryItemDto>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Xóa mềm một inventory item khỏi kho của user.
    /// </summary>
    /// <param name="id">Id inventory item</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var success = await _inventoryItemService.DeleteAsync(id);
            if (!success)
            {
                _logger.Warn($"[InventoryItemController][Delete] Không tìm thấy inventory item {id}");
                return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy inventory item."));
            }

            _logger.Success($"[InventoryItemController][Delete] Xóa inventory item {id} thành công.");
            return Ok(ApiResult<object>.Success(null, "200", "Xóa inventory item thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][Delete] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Yêu cầu giao hàng cho một InventoryItem.
    ///     Nếu chưa có địa chỉ thì phải truyền vào addressId.
    ///     Tạo Shipment và cập nhật trạng thái OrderDetail liên quan.
    /// </summary>
    [HttpPost("{id:guid}/request-shipment")]
    [ProducesResponseType(typeof(ApiResult<ShipResponseDTO>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> RequestShipment(Guid id, [FromBody] RequestShipmentDTO request)
    {
        try
        {
            var result = await _inventoryItemService.RequestShipmentAsync(id, request);
            _logger.Success($"[InventoryItemController][RequestShipment] Đã tạo yêu cầu giao hàng cho item {id}.");
            return Ok(ApiResult<ShipResponseDTO>.Success(result, "200", "Yêu cầu giao hàng thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][RequestShipment] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }
}