using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/shipments")]
public class ShipmentController : ControllerBase
{
    private readonly IShipmentService _shipmentService;
    private readonly ILoggerService _logger;

    public ShipmentController(IShipmentService shipmentService, ILoggerService logger)
    {
        _shipmentService = shipmentService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy chi tiết shipment theo Id (chỉ cho phép user là chủ đơn hàng).
    /// </summary>
    [Authorize]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult<ShipmentDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _shipmentService.GetByIdAsync(id);
            return Ok(ApiResult<ShipmentDto>.Success(result, "200", "Lấy thông tin shipment thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ShipmentDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy danh sách shipment của user hiện tại (có thể filter theo orderId hoặc orderDetailId).
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<List<ShipmentDto>>), 200)]
    public async Task<IActionResult> GetMyShipments([FromQuery] Guid? orderId = null,
        [FromQuery] Guid? orderDetailId = null)
    {
        try
        {
            var result = await _shipmentService.GetMyShipmentsAsync(orderId, orderDetailId);
            return Ok(ApiResult<List<ShipmentDto>>.Success(result, "200", "Lấy danh sách shipment thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<List<ShipmentDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy shipment theo orderDetailId (không kiểm tra user).
    /// </summary>
    [Authorize]
    [HttpGet("by-order-detail/{orderDetailId}")]
    [ProducesResponseType(typeof(ApiResult<List<ShipmentDto>>), 200)]
    public async Task<IActionResult> GetByOrderDetailId(Guid orderDetailId)
    {
        try
        {
            var result = await _shipmentService.GetByOrderDetailIdAsync(orderDetailId);
            return Ok(ApiResult<List<ShipmentDto>>.Success(result, "200",
                "Lấy shipment theo order detail thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<List<ShipmentDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}