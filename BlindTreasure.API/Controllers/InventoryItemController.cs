using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
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
            }, "200", "Danh sách vật phẩm trong kho của bạn đã được tải thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][GetMyInventory] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Tạo mới một inventory item cho user hiện tại.
    ///     (Chỉ sử dụng cho mục đích nội bộ, không public cho client)
    /// </summary>
    /// <param name="dto">Thông tin inventory item cần tạo</param>
    /// <returns>Inventory item vừa được tạo</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<InventoryItemDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemDto dto)
    {
        try
        {
            var result = await _inventoryItemService.CreateAsync(dto, null);
            _logger.Success(
                $"[InventoryItemController][Create] Tạo inventory item mới thành công cho product {dto.ProductId}.");
            return Ok(ApiResult<InventoryItemDto>.Success(result, "200", "Vật phẩm trong kho đã được tạo thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][Create] {ex.Message}");
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
                "Các vật phẩm đã mở từ Blind Box đã được tải thành công."));
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
                return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy vật phẩm trong kho."));
            }

            _logger.Info($"[InventoryItemController][GetById] Lấy chi tiết inventory item {id} thành công.");
            return Ok(ApiResult<InventoryItemDto>.Success(item, "200",
                "Thông tin chi tiết vật phẩm trong kho đã được tải thành công."));
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
            return Ok(ApiResult<InventoryItemDto>.Success(result, "200",
                "Vật phẩm trong kho đã được cập nhật thành công."));
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
                return NotFound(ApiResult<object>.Failure("404", "Không tìm thấy vật phẩm trong kho để xóa."));
            }

            _logger.Success($"[InventoryItemController][Delete] Xóa inventory item {id} thành công.");
            return Ok(ApiResult<object>.Success(null, "200", "Vật phẩm trong kho đã được xóa thành công."));
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
    ///     Yêu cầu giao hàng cho một LIST InventoryItem truyền vào
    ///     LẤY ĐỊA CHỈ MẶC ĐỊNH CỦA NGƯỜI DÙNG
    ///     Tạo Shipment và cập nhật trạng thái OrderDetail liên quan.
    /// </summary>
    [HttpPost("request-shipment")]
    [ProducesResponseType(typeof(ApiResult<ShipmentItemResponseDTO>), 200)]
    [ProducesResponseType(typeof(ApiResult<ShipmentItemResponseDTO>), 400)]
    public async Task<IActionResult> RequestShipment([FromBody] RequestItemShipmentDTO request)
    {
        try
        {
            var result = await _inventoryItemService.RequestShipmentAsync(request);
            _logger.Success(
                $"[InventoryItemController][RequestShipment] Đã tạo yêu cầu giao hàng cho list item {request.InventoryItemIds}.");
            return Ok(ApiResult<ShipmentItemResponseDTO>.Success(result, "200",
                "Yêu cầu giao hàng đã được tạo thành công."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[InventoryItemController][RequestShipment] {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }


    /// <summary>
    ///     PREVIEW yêu cầu giao hàng cho một LIST InventoryItem.
    ///     LẤY ĐỊA CHỈ MẶC ĐỊNH CỦA NGƯỜI DÙNG
    ///     Tạo Shipment và cập nhật trạng thái OrderDetail liên quan.
    /// </summary>
    [HttpPost("preview-shipment")]
    [ProducesResponseType(typeof(ApiResult<List<ShipmentCheckoutResponseDTO>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> RequestPreviewShipmentForListItems([FromBody] RequestItemShipmentDTO requests)
    {
        try
        {
            var result = await _inventoryItemService.PreviewShipmentForListItemsAsync(requests);
            _logger.Success(
                $"[RequestPreviewShipmentForListItems][RequestShipment] Đã tạo yêu cầu preview đơn giao hàng cho list item: {requests.InventoryItemIds}.");
            return Ok(ApiResult<List<ShipmentCheckoutResponseDTO>>.Success(result, "200",
                "Xem trước đơn hàng vận chuyển đã được tạo thành công."));
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