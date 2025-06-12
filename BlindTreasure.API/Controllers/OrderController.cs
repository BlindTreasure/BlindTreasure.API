using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Text;
using Stripe.Checkout;
using BlindTreasure.Application.Interfaces.Commons;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILoggerService _logger;
    private readonly ITransactionService _transactionService;


    public OrderController(IOrderService orderService, ILoggerService logger, ITransactionService transactionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));    
        _orderService = orderService;
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
    }

    /// <summary>
    /// Đặt hàng (checkout) từ giỏ hàng hiện tại, trả về link thanh toán Stripe.
    /// </summary>
    /// <param name="dto">Thông tin đặt hàng (địa chỉ giao hàng, ...)</param>
    /// <returns>Link thanh toán Stripe cho đơn hàng vừa tạo</returns>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> Checkout([FromBody] CreateOrderDto dto)
    {
        try
        {
            var paymentUrl = await _orderService.CheckoutAsync(dto);
            return Ok(ApiResult<string>.Success(paymentUrl, "200", "Đặt hàng thành công. Chuyển hướng đến thanh toán."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy chi tiết một đơn hàng của user hiện tại.
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
    /// Lấy danh sách đơn hàng của user hiện tại.
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
    /// Hủy một đơn hàng (chỉ khi trạng thái cho phép).
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
    /// Xóa mềm một đơn hàng (user chỉ xóa được đơn của mình).
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

    /// <summary>
    /// Stripe webhook callback: xử lý sự kiện thanh toán từ Stripe (checkout.session.completed, checkout.session.expired, ...)
    /// </summary>
    [HttpPost("checkout-callback-handler")]
    public async Task<IActionResult> HandleChechoutWebhook()
    {
        _logger.Info("Stripe webhook received.");

        var json = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                "whsec_1922024ed268f46c73bfac2bd2bab31e490189a882ec21e458c387b0f8ed8b13" // TODO: Move to config/secret
            );

            _logger.Info($"Received event type: {stripeEvent.Type}");
            var session = stripeEvent.Data.Object as Session ?? throw ErrorHelper.NotFound("Stripe session not found.");

            switch (stripeEvent.Type)
            {
                case "checkout.session.expired":
                    _logger.Warn("Checkout session expired.");
                    await HandleExpiredCheckoutSession(session);
                    break;

                case "checkout.session.completed":
                    _logger.Info("Async payment succeeded.");
                    await HandleSuccessfulPayment(session);
                    _logger.Info("Payment Intent created, Id: " + session.PaymentIntentId);
                    await HandlePaymentIntentCreatedSession(session);
                    break;

                default:
                    _logger.Warn($"Unhandled event type: {stripeEvent.Type}");
                    break;
            }
            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.Error($"Stripe exception: {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.Error("Unhandled exception in Stripe webhook.: " +ex.Message );
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    private async Task HandleSuccessfulPayment(Session session)
    {
        try
        {
            // Lấy orderId từ metadata (nếu có)
            string orderId = session.Metadata != null && session.Metadata.TryGetValue("orderId", out var orderIdStr)
                ? orderIdStr
                : null;

            if (string.IsNullOrEmpty(session.Id))
                throw ErrorHelper.WithStatus(400, "Session Id is missing.");

            // Gọi TransactionService để xử lý thành công
            if (!string.IsNullOrEmpty(orderId))
            {
                await _transactionService.HandleSuccessfulPaymentAsync(session.Id, orderId);
            }
            else
            {
                _logger.Warn("OrderId not found in Stripe session metadata.");
            }
        }
        catch (StripeException ex)
        {
            _logger.Error("StripeException while handling payment: " + ex.Message);
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
        }
        catch (Exception ex)
        {
            _logger.Error("Unhandled exception while handling payment: " + ex.Message);
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
        }
    }

    private async Task HandleExpiredCheckoutSession(Session session)
    {
        await _transactionService.HandleFailedPaymentAsync(session.Id);
    }

    private async Task HandlePaymentIntentCreatedSession(Session session)
    {
        try
        {
            if (!string.IsNullOrEmpty(session.PaymentIntentId))
            {
                await _transactionService.HandlePaymentIntentCreatedAsync(session.PaymentIntentId, session.Id);
            }
        }
        catch (StripeException e)
        {
            _logger.Error("StripeException while handling payment intent.:"+e);
        }
        catch (Exception e)
        {
            _logger.Error("Unhandled exception while handling payment intent: " + e.Message);
        }
    }
}