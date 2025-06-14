using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    ///     Đặt hàng (checkout) từ giỏ hàng hiện tại.
    /// </summary>
    /// <param name="dto">Thông tin đặt hàng (địa chỉ giao hàng, ...)</param>
    /// <returns>Thông tin đơn hàng vừa tạo</returns>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(ApiResult<OrderDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> Checkout([FromBody] CreateOrderDto dto)
    {
        try
        {
            var result = await _orderService.CheckoutAsync(dto);
            return Ok(ApiResult<OrderDto>.Success(result, "200", "Đặt hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<OrderDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy chi tiết một đơn hàng của user hiện tại.
    /// </summary>
    /// <param name="orderId">Id đơn hàng</param>
    /// <returns>Chi tiết đơn hàng</returns>
    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(ApiResult<OrderDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetOrderById(Guid orderId)
    {
        try
        {
            var result = await _orderService.GetOrderByIdAsync(orderId);
            return Ok(ApiResult<OrderDto>.Success(result, "200", "Lấy chi tiết đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<OrderDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy danh sách đơn hàng của user hiện tại.
    /// </summary>
    /// <returns>Danh sách đơn hàng</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<List<OrderDto>>), 200)]
    public async Task<IActionResult> GetMyOrders()
    {
        try
        {
            var result = await _orderService.GetMyOrdersAsync();
            return Ok(ApiResult<List<OrderDto>>.Success(result, "200", "Lấy danh sách đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<List<OrderDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Hủy một đơn hàng (chỉ khi trạng thái cho phép).
    /// </summary>
    /// <param name="orderId">Id đơn hàng</param>
    [HttpPut("{orderId}/cancel")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> CancelOrder(Guid orderId)
    {
        try
        {
            await _orderService.CancelOrderAsync(orderId);
            return Ok(ApiResult<object>.Success(null, "200", "Đã hủy đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Xóa mềm một đơn hàng (user chỉ xóa được đơn của mình).
    /// </summary>
    /// <param name="orderId">Id đơn hàng</param>
    [HttpDelete("{orderId}")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteOrder(Guid orderId)
    {
        try
        {
            await _orderService.DeleteOrderAsync(orderId);
            return Ok(ApiResult<object>.Success(null, "200", "Đã xóa đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}