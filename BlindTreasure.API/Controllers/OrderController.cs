using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly ILoggerService _logger;
    private readonly IOrderService _orderService;
    private readonly ITransactionService _transactionService;
    private readonly IOrderDetailInventoryItemLogService _orderDetailInventoryItemLogService;


    public OrderController(IOrderService orderService, ILoggerService logger, ITransactionService transactionService, IOrderDetailInventoryItemLogService orderDetailInventoryItemLogService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orderService = orderService;
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _orderDetailInventoryItemLogService = orderDetailInventoryItemLogService ?? throw new ArgumentNullException(nameof(orderDetailInventoryItemLogService));
    }

    [Authorize]
    /// <summary>
    ///     Đặt hàng (checkout) từ cart truyền lên từ client, trả về link thanh toán Stripe.
    /// </summary>
    /// <param name="cart">Cart truyền từ FE (danh sách sản phẩm, số lượng, giá, ...)</param>
    /// <returns>Link thanh toán Stripe cho đơn hàng vừa tạo</returns>
    [HttpPost("checkout-direct")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> CheckoutDirect([FromBody] DirectCartCheckoutDto cart)
    {
        try
        {
            var paymentUrl = await _orderService.CheckoutFromClientCartAsync(cart);
            return Ok(ApiResult<MultiOrderCheckoutResultDto>.Success(paymentUrl, "200",
                "Đặt hàng thành công. Chuyển hướng đến thanh toán."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [Authorize]
    /// <summary>
    ///     Đặt hàng (checkout) từ giỏ hàng hiện tại, trả về link thanh toán Stripe.
    /// </summary>
    /// <param name="dto">Thông tin đặt hàng (địa chỉ giao hàng, ...)</param>
    /// <returns>Link thanh toán Stripe cho đơn hàng vừa tạo</returns>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> Checkout([FromBody] CreateCheckoutRequestDto dto)
    {
        try
        {
            var paymentUrl = await _orderService.CheckoutAsync(dto);
            return Ok(ApiResult<MultiOrderCheckoutResultDto>.Success(paymentUrl, "200",
                "Đặt hàng thành công. Chuyển hướng đến thanh toán."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy chi tiết một đơn hàng của user hiện tại.
    /// </summary>
    /// <param name="orderId">Id đơn hàng</param>
    /// <returns>Chi tiết đơn hàng</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult<OrderDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        try
        {
            var result = await _orderService.GetOrderByIdAsync(id);
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
    ///     Lấy danh sách chi tiết đơn hàng của user hiện tại (có phân trang, filter).
    /// </summary>
    /// <returns>Danh sách chi tiết đơn hàng phân trang</returns>
    [Authorize]
    [HttpGet("order-details")]
    [ProducesResponseType(typeof(ApiResult<Pagination<OrderDetailDto>>), 200)]
    public async Task<IActionResult> GetMyOrderDetails([FromQuery] OrderDetailQueryParameter param)
    {
        try
        {
            var result = await _orderService.GetMyOrderDetailsAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách chi tiết đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy danh sách đơn hàng của user hiện tại (có phân trang, filter).
    /// </summary>
    /// <returns>Danh sách đơn hàng phân trang</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<OrderDto>>), 200)]
    public async Task<IActionResult> GetMyOrders([FromQuery] OrderQueryParameter param)
    {
        try
        {
            var result = await _orderService.GetMyOrdersAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Hủy một đơn hàng (chỉ khi trạng thái cho phép).
    /// </summary>
    /// <param name="id">Id đơn hàng</param>
    [HttpPut("{id}/cancel")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        try
        {
            await _orderService.CancelOrderAsync(id);
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
    /// <param name="id">Id đơn hàng</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        try
        {
            await _orderService.DeleteOrderAsync(id);
            return Ok(ApiResult<object>.Success(null, "200", "Đã xóa đơn hàng thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy danh sách đơn hàng theo groupId (không phân trang).
    /// </summary>
    /// <param name="groupId">Id của nhóm đơn hàng (CheckoutGroupId)</param>
    /// <returns>Danh sách đơn hàng thuộc group</returns>
    [Authorize]
    [HttpGet("group/{groupid}")]
    [ProducesResponseType(typeof(ApiResult<List<OrderDto>>), 200)]
    public async Task<IActionResult> GetOrdersByGroupId(Guid groupId)
    {
        try
        {
            var result = await _orderService.GetOrderByCheckoutGroupId(groupId);
            return Ok(ApiResult<List<OrderDto>>.Success(result, "200", "Lấy danh sách đơn hàng theo group thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<List<OrderDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy danh sách log của OrderDetail theo Id.
    /// </summary>
    [Authorize]
    [HttpGet("order-details/{id}/logs")]
    [ProducesResponseType(typeof(ApiResult<List<OrderDetailInventoryItemLogDto>>), 200)]
    public async Task<IActionResult> GetOrderDetailLogs(Guid id)
    {
        try
        {
            var logs = await _orderDetailInventoryItemLogService.GetLogByOrderDetailIdAsync(id);
            return Ok(ApiResult<List<OrderDetailInventoryItemLogDto>>.Success(logs, "200", "Lấy log của OrderDetail thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<List<OrderDetailInventoryItemLogDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy danh sách log của InventoryItem theo Id.
    /// </summary>
    [Authorize]
    [HttpGet("inventory-items/{id}/logs")]
    [ProducesResponseType(typeof(ApiResult<List<OrderDetailInventoryItemLogDto>>), 200)]
    public async Task<IActionResult> GetInventoryItemLogs(Guid id)
    {
        try
        {
            var logs = await _orderDetailInventoryItemLogService.GetLogByInventoryItemIdAsync(id);
            return Ok(ApiResult<List<OrderDetailInventoryItemLogDto>>.Success(logs, "200", "Lấy log của InventoryItem thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<List<OrderDetailInventoryItemLogDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}