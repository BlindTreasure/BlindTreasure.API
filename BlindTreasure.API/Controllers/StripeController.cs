using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.DTOs.StripeDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Text;

namespace BlindTreasure.API.Controllers;

/// <summary>
///     Stripe payment, payout, refund, and account management APIs for BlindTreasure marketplace.
/// </summary>
[Route("api/stripe")]
[ApiController]
public class StripeController : ControllerBase
{
    private readonly ICartItemService _cartItemService;

    private readonly IClaimsService _claimService;
    private readonly IConfiguration _configuration;
    private readonly string _deployStripeSecret;
    private readonly string _localStripeSecret;
    private readonly ILoggerService _logger;
    private readonly IOrderService _orderService;
    private readonly ISellerService _sellerService;
    private readonly IStripeClient _stripeClient;
    private readonly IStripeService _stripeService;
    private readonly ITransactionService _transactionService;
    private readonly IUserService _userService;


    public StripeController(
        ISellerService sellerService,
        IClaimsService claimService,
        IUserService userService,
        IStripeService stripeService,
        ILoggerService logger,
        IStripeClient stripeClient,
        IOrderService orderService,
        ITransactionService transactionService,
        IConfiguration configuration,
        ICartItemService cartItemService)
    {
        _claimService = claimService;
        _userService = userService;
        _stripeService = stripeService;
        _logger = logger;
        _stripeClient = stripeClient;
        _orderService = orderService;
        _transactionService = transactionService;
        _sellerService = sellerService;
        _configuration = configuration;
        _localStripeSecret = _configuration["STRIPE:LocalWebhookSecret"] ?? "";
        _deployStripeSecret = _configuration["STRIPE:DeployWebhookSecret"] ?? "";
        _cartItemService = cartItemService;
    }

    /// <summary>
    ///     Tạo đơn hàng và trả về link thanh toán Stripe cho đơn hàng từ cart truyền lên từ client.
    /// </summary>
    /// <param name="cart">Thông tin cart từ FE (danh sách sản phẩm, số lượng, giá...)</param>
    /// <returns>Link thanh toán Stripe cho đơn hàng vừa tạo</returns>
    [Authorize]
    [HttpPost("checkout-direct")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> CheckoutDirect([FromBody] DirectCartCheckoutDto cart)
    {
        _logger.Info("[Stripe][CheckoutDirect] Bắt đầu tạo đơn hàng từ cart client.");
        try
        {
            var paymentUrl = await _orderService.CheckoutFromClientCartAsync(cart);
            _logger.Success("[Stripe][CheckoutDirect] Đặt hàng thành công, trả về link thanh toán.");
            return Ok(ApiResult<MultiOrderCheckoutResultDto>.Success(paymentUrl, "200",
                "Đặt hàng thành công. Chuyển hướng đến thanh toán."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][CheckoutDirect] Lỗi: {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Tạo đơn hàng từ giỏ hàng trong DB và trả về link thanh toán Stripe (tiến test).
    /// </summary>
    /// <param name="dto">Thông tin đặt hàng (địa chỉ giao hàng, ...)</param>
    /// <returns>Link thanh toán Stripe cho đơn hàng vừa tạo</returns>
    [Authorize]
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> Checkout([FromBody] CreateCheckoutRequestDto dto)
    {
        _logger.Info("[Stripe][Checkout] Bắt đầu tạo đơn hàng từ giỏ hàng.");
        try
        {
            var paymentUrl = await _orderService.CheckoutAsync(dto);
            _logger.Success("[Stripe][Checkout] Đặt hàng thành công, trả về link thanh toán.");
            return Ok(ApiResult<MultiOrderCheckoutResultDto>.Success(paymentUrl, "200",
                "Đặt hàng thành công. Chuyển hướng đến thanh toán."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][Checkout] Lỗi: {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     TIẾN TEST BẰNG CÁI NÀY
    /// </summary>
    [Authorize]
    [HttpPost("preview-shipping")]
    public async Task<IActionResult> PreviewShippingFromCart()
    {
        try
        {
            var cart = await _cartItemService.GetCurrentUserCartAsync();
            var result = await _orderService.PreviewShippingCheckoutAsync(cart.SellerItems);
            return Ok(ApiResult<List<ShipmentCheckoutResponseDTO>>.Success(result, "200",
                "Preview shipment thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     FRONT-END DÙNG API NÀY ĐỂ LẤY TRƯỚC THÔNG TIN GIAO HÀNG CỦA ITEM TRUYỀN VÀO
    /// </summary>
    /// <param name="cart">Thông tin cart từ FE (danh sách sản phẩm, số lượng, giá...)</param>
    /// <returns>Link thanh toán Stripe cho đơn hàng vừa tạo</returns>
    [Authorize]
    [HttpPost("preview-shipping-direct")]
    public async Task<IActionResult> PreviewShippingFromClientCart([FromBody] DirectCartCheckoutDto cart)
    {
        try
        {
            var result = await _orderService.PreviewShippingCheckoutAsync(cart.SellerItems);
            return Ok(ApiResult<List<ShipmentCheckoutResponseDTO>>.Success(result, "200",
                "Preview shipment thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Stripe webhook callback: xử lý sự kiện thanh toán từ Stripe(checkout.session.completed, checkout.session.expired,
    ///     ...).
    /// </summary>
    [HttpPost("checkout-callback-handler")]
    public async Task<IActionResult> HandleChechoutWebhook()
    {
        _logger.Info("[Stripe][Webhook] Nhận sự kiện webhook từ Stripe.");
        var json = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();

        Event? stripeEvent = null;
        Exception? lastEx = null;

        foreach (var secret in new[] { _localStripeSecret, _deployStripeSecret })
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    secret
                );
                _logger.Info($"[Stripe][Webhook] Đã xác thực thành công với secret: {secret.Substring(0, 12)}...");
                break;
            }
            catch (StripeException ex)
            {
                lastEx = ex;
                _logger.Warn($"[Stripe][Webhook] Sai secret thử: {secret.Substring(0, 12)}... - {ex.Message}");
            }

        if (stripeEvent == null)
        {
            _logger.Error("[Stripe][Webhook] Không xác thực được Stripe webhook với bất kỳ secret nào.");
            var statusCode = ExceptionUtils.ExtractStatusCode(lastEx);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(lastEx);
            return StatusCode(statusCode, errorResponse);
        }

        _logger.Info($"[Stripe][Webhook] Nhận event: {stripeEvent.Type}");
        try
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.expired":
                    var expiredSession = stripeEvent.Data.Object as Session
                                         ?? throw ErrorHelper.NotFound("Stripe session not found.");
                    _logger.Warn("[Stripe][Webhook] Checkout session expired, Session ID: " + expiredSession.Id);
                    await HandleExpiredCheckoutSession(expiredSession);
                    break;

                case "checkout.session.completed":
                    var completedSession = stripeEvent.Data.Object as Session
                                           ?? throw ErrorHelper.NotFound("Stripe session not found.");
                    _logger.Info("[Stripe][Webhook] Thanh toán thành công, Session ID: " + completedSession.Id);

                    // Xử lý thanh toán nhiều order
                    if (completedSession.Metadata != null &&
                        completedSession.Metadata.TryGetValue("isGeneralPayment", out var isGeneral) &&
                        isGeneral == "true" &&
                        completedSession.Metadata.TryGetValue("orderIds", out var orderIdsStr))
                    {
                        var orderIds = orderIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                            .Where(guid => guid != Guid.Empty)
                            .ToList();

                        foreach (var orderId in orderIds)
                            await HandleSuccessfulPaymentForOrder(orderId, completedSession.Id,
                                completedSession.Metadata);


                    }
                    else
                    {
                        await HandleSuccessfulPayment(completedSession);
                    }

                    await HandleSuccessfulItemShipmentRequestPayment(completedSession);
                    await HandlePaymentIntentCreatedSession(completedSession);
                    break;

                default:
                    _logger.Warn($"[Stripe][Webhook] Bỏ qua event không xử lý: {stripeEvent.Type}");
                    break;
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.Error($"[Stripe][Webhook] Stripe exception: {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][Webhook] Unhandled exception: {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Tạo hoặc lấy lại link thanh toán bằng cách truyền groupid của nhóm order vào
    /// </summary>
    [Authorize]
    [HttpPost("group-payment-link")]
    public async Task<IActionResult> GetGroupPaymentLink([FromBody] GetCheckoutGroupLinkDto request)
    {
        try
        {
            var url = await _stripeService.GetOrCreateGroupPaymentLink(request.CheckoutGroupId);
            return Ok(ApiResult<string>.Success(url, "200", "Link thanh toán nhóm đã được tạo hoặc lấy lại."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][GroupPaymentLink] Lỗi: {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure(ex.Message));
        }
    }

    /// <summary>
    ///     Lấy onboarding link Stripe Express cho seller để hoàn tất xác minh tài khoản Stripe.
    /// </summary>
    /// <param name="redirectUrl">URL chuyển hướng sau khi seller hoàn thành onboarding (tùy chọn)</param>
    /// <returns>Onboarding link Stripe Express</returns>
    [HttpGet("onboarding-link")]
    [Authorize(Roles = "Seller,Admin,Staff")]
    public async Task<IActionResult> GetOnboardingLink()
    {
        _logger.Info("[Stripe][OnboardingLink] Lấy onboarding link cho seller.");
        try
        {
            var userId = _claimService.CurrentUserId;
            var seller = await _sellerService.GetSellerProfileByUserIdAsync(userId);
            if (seller == null)
            {
                _logger.Warn("[Stripe][OnboardingLink] Seller not found.");
                return NotFound(ApiResult<object>.Failure("Seller not found."));
            }

            var url = await _stripeService.GenerateSellerOnboardingLinkAsync(seller.SellerId);
            _logger.Success("[Stripe][OnboardingLink] Onboarding link generated.");
            return Ok(ApiResult<string>.Success(url, "200", "Onboarding link generated."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][OnboardingLink] Lỗi: {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure(ex.Message));
        }
    }

    /// <summary>
    ///     Lấy login link Stripe Express cho seller để đăng nhập vào dashboard Stripe.
    /// </summary>
    [HttpGet("login-link")]
    [Authorize(Roles = "Seller,Admin,Staff")]
    public async Task<IActionResult> GetLoginLink()
    {
        _logger.Info("[Stripe][LoginLink] Lấy login link cho seller.");
        try
        {
            var url = await _stripeService.GenerateExpressLoginLink();
            _logger.Success("[Stripe][LoginLink] Login link generated.");
            return Ok(ApiResult<string>.Success(url, "200", "Login link generated."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][LoginLink] Lỗi: {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure(ex.Message));
        }
    }

    /// <summary>
    ///     Thực hiện payout chuyển tiền cho seller (Stripe Connect). Chỉ cho phép Admin/Staff.
    /// </summary>
    /// <param name="dto">Thông tin payout (StripeAccountId, số tiền, currency, mô tả)</param>
    /// <returns>Thông tin Stripe Transfer</returns>
    [HttpPost("payout")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> PayoutToSeller([FromBody] PayoutRequestDto dto)
    {
        _logger.Info(
            $"[Stripe][Payout] Thực hiện payout cho seller: {dto.SellerStripeAccountId}, amount: {dto.Amount} {dto.Currency}");
        try
        {
            var transfer = await _stripeService.PayoutToSellerAsync(dto.SellerStripeAccountId, dto.Amount, dto.Currency,
                dto.Description);
            _logger.Success("[Stripe][Payout] Payout thành công.");
            return Ok(ApiResult<object>.Success(transfer, "200", "Payout successful."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][Payout] Lỗi: {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure(ex.Message));
        }
    }

    /// <summary>
    ///     Hoàn tiền cho khách hàng (Stripe Refund). Chỉ cho phép Admin/Staff.
    /// </summary>
    /// <param name="dto">Thông tin refund (PaymentIntentId, số tiền)</param>
    /// <returns>Thông tin Stripe Refund</returns>
    [HttpPost("refund")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> RefundPayment([FromBody] RefundRequestDto dto)
    {
        _logger.Info(
            $"[Stripe][Refund] Thực hiện refund cho paymentIntent: {dto.PaymentIntentId}, amount: {dto.Amount}");
        try
        {
            var refund = await _stripeService.RefundPaymentAsync(dto.PaymentIntentId, dto.Amount);
            _logger.Success("[Stripe][Refund] Refund thành công.");
            return Ok(ApiResult<object>.Success(refund, "200", "Refund successful."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][Refund] Lỗi: {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure(ex.Message));
        }
    }

    /// <summary>
    ///     Đảo ngược payout (Stripe Transfer Reversal). Chỉ cho phép Admin/Staff.
    /// </summary>
    /// <param name="dto">Thông tin reversal (TransferId)</param>
    /// <returns>Thông tin Stripe TransferReversal</returns>
    [HttpPost("reverse-payout")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> ReversePayout([FromBody] ReversePayoutRequestDto dto)
    {
        _logger.Info($"[Stripe][ReversePayout] Đảo ngược payout transferId: {dto.TransferId}");
        try
        {
            var reversal = await _stripeService.ReversePayoutAsync(dto.TransferId);
            _logger.Success("[Stripe][ReversePayout] Đảo ngược payout thành công.");
            return Ok(ApiResult<object>.Success(reversal, "200", "Payout reversed."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][ReversePayout] Lỗi: {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure(ex.Message));
        }
    }

    /// <summary>
    ///     Kiểm tra trạng thái xác minh Stripe account của seller (đủ điều kiện nhận tiền hay chưa).
    /// </summary>
    /// <param name="sellerStripeAccountId">Stripe Account Id của seller</param>
    /// <returns>True nếu đã xác minh, false nếu chưa</returns>
    [HttpGet("verify-seller-account")]
    [Authorize(Roles = "Seller,Admin,Staff")]
    public async Task<IActionResult> IsSellerStripeAccountVerified([FromQuery] string sellerStripeAccountId)
    {
        _logger.Info($"[Stripe][VerifySellerAccount] Kiểm tra xác minh Stripe account: {sellerStripeAccountId}");
        try
        {
            var isVerified = await _stripeService.IsSellerStripeAccountVerifiedAsync(sellerStripeAccountId);
            _logger.Success("[Stripe][VerifySellerAccount] Trạng thái xác minh Stripe account: " + isVerified);
            return Ok(ApiResult<bool>.Success(isVerified, "200", "Stripe account verification status checked."));
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][VerifySellerAccount] Lỗi: {ex.Message}");
            return StatusCode(500, ApiResult<object>.Failure(ex.Message));
        }
    }

    /// <summary>
    ///     Stripe webhook callback: xử lý sự kiện refund từ Stripe (charge.refunded, refund.updated, refund.failed).
    /// </summary>
    [HttpPost("refund-callback-handler")]
    public async Task<IActionResult> HandleRefundWebhook()
    {
        _logger.Info("[Stripe][Webhook] Nhận sự kiện webhook từ Stripe.");
        var json = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();

        // Fallback 2 secret: local và deploy
        var localSecret = "whsec_1922024ed268f46c73bfac2bd2bab31e490189a882ec21e458c387b0f8ed8b13";
        var deploySecret = "whsec_uWjfI4fkQ7zbwE8VrWMcu2Ysyqm8heUh";
        Event? stripeEvent = null;
        Exception? lastEx = null;

        foreach (var secret in new[] { localSecret, deploySecret })
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    secret
                );
                _logger.Info($"[Stripe][Webhook] Đã xác thực thành công với secret: {secret.Substring(0, 12)}...");
                break;
            }
            catch (StripeException ex)
            {
                lastEx = ex;
                _logger.Warn($"[Stripe][Webhook] Sai secret thử: {secret.Substring(0, 12)}... - {ex.Message}");
            }

        if (stripeEvent == null)
        {
            _logger.Error("[Stripe][Webhook] Không xác thực được Stripe webhook với bất kỳ secret nào.");
            var statusCode = ExceptionUtils.ExtractStatusCode(lastEx);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(lastEx);
            return StatusCode(statusCode, errorResponse);
        }

        _logger.Info($"[Stripe][Webhook] Nhận event: {stripeEvent.Type}");
        var session = stripeEvent.Data.Object as Session ?? throw ErrorHelper.NotFound("Stripe session not found.");

        try
        {
            switch (stripeEvent.Type)
            {
                case "charge.refunded":
                    var chargeRefunded = stripeEvent.Data.Object as Charge;
                    // await _transactionService.HandleSuccessfulRefund(chargeRefunded.PaymentIntentId);
                    _logger.Info($"[Stripe][RefundWebhook] Charge {chargeRefunded?.Id} refunded.");
                    return Ok("refund successfully");
                case "refund.updated":
                    var refundUpdated = stripeEvent.Data.Object as Refund;
                    _logger.Info(
                        $"[Stripe][RefundWebhook] Refund {refundUpdated?.Id} updated: {refundUpdated?.Status}");
                    break;
                case "refund.failed":
                    var refundFailed = stripeEvent.Data.Object as Refund;
                    _logger.Error($"[Stripe][RefundWebhook] Refund {refundFailed?.Id} failed.");
                    break;
                default:
                    _logger.Warn($"[Stripe][RefundWebhook] Unhandled event type: {stripeEvent.Type}");
                    break;
            }

            return Ok();
        }
        catch (StripeException e)
        {
            _logger.Error($"[Stripe][RefundWebhook] Stripe exception: {e.Message}");
            return BadRequest();
        }
    }

    #region Private Handlers

    /// <summary>
    ///     Xử lý khi thanh toán thành công từ Stripe webhook.
    /// </summary>
    private async Task HandleSuccessfulPayment(Session session)
    {
        try
        {
            var orderId = session.Metadata != null && session.Metadata.TryGetValue("orderId", out var orderIdStr)
                ? orderIdStr
                : null;

            if (string.IsNullOrEmpty(session.Id))
                throw ErrorHelper.WithStatus(400, "Session Id is missing.");

            if (!string.IsNullOrEmpty(orderId))
            {
                await _transactionService.HandleSuccessfulPaymentAsync(session.Id, orderId);
                _logger.Success(
                    $"[Stripe][Webhook] Thanh toán thành công cho orderId: {orderId}, sessionId: {session.Id}");

                // Clean up Stripe coupon nếu có
                if (session.Metadata != null &&
                    session.Metadata.TryGetValue("couponId", out var couponId) &&
                    !string.IsNullOrWhiteSpace(couponId))
                {
                    _logger.Info($"[Stripe][Webhook] Cleanup Stripe coupon: {couponId}");
                    await _stripeService.CleanupStripeCoupon(couponId);
                    _logger.Success($"[Stripe][Webhook] Đã xóa coupon Stripe: {couponId}");
                }
            }
            else
            {
                _logger.Warn("[Stripe][Webhook] OrderId not found in Stripe session metadata.");
            }
        }
        catch (StripeException ex)
        {
            _logger.Error("[Stripe][Webhook] StripeException while handling payment: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Error("[Stripe][Webhook] Unhandled exception while handling payment: " + ex.Message);
        }
    }

    /// <summary>
    ///     Xử lý khi checkout session hết hạn.
    /// </summary>
    private async Task HandleExpiredCheckoutSession(Session session)
    {
        if (session.PaymentStatus == "unpaid" && session.Status != "expired" && DateTime.UtcNow < session.ExpiresAt)
        {
            _logger.Info($"[Stripe][Webhook] Checkout session not expired yet: {session.Id}");
            return;
        }


            _logger.Warn($"[Stripe][Webhook] Checkout session expired: {session.Id}");
        await _transactionService.HandleFailedPaymentAsync(session.Id);
    }

    /// <summary>
    ///     Xử lý khi Stripe tạo PaymentIntent (sau khi thanh toán thành công).
    /// </summary>
    private async Task HandlePaymentIntentCreatedSession(Session session)
    {
        try
        {
            if (!string.IsNullOrEmpty(session.PaymentIntentId))
            {
                var couponId = session.Metadata != null &&
                               session.Metadata.TryGetValue("couponId", out var CouponIdStr)
                    ? CouponIdStr
                    : null;

                _logger.Info(
                    $"[Stripe][Webhook] PaymentIntent created: {session.PaymentIntentId}, sessionId: {session.Id}");
                await _transactionService.HandlePaymentIntentCreatedAsync(session.PaymentIntentId, session.Id,
                    couponId);
            }
        }
        catch (StripeException e)
        {
            _logger.Error("[Stripe][Webhook] StripeException while handling payment intent: " + e.Message);
        }
        catch (Exception e)
        {
            _logger.Error("[Stripe][Webhook] Unhandled exception while handling payment intent: " + e.Message);
        }
    }

    /// <summary>
    ///     Xử lý khi thanh toán thành công từ Stripe webhook.
    /// </summary>
    private async Task HandleSuccessfulItemShipmentRequestPayment(Session session)
    {
        try
        {
            var isShipment = session.Metadata != null &&
                             session.Metadata.TryGetValue("IsShipmenRequest", out var isShipmentStr)
                ? isShipmentStr
                : null;
            if (string.IsNullOrEmpty(isShipment) || isShipment != "True")
            {
                _logger.Warn("[Stripe][Webhook] Not a shipment payment, skipping.");
                return;
            }

            var shipmentIdsStr = session.Metadata != null &&
                                 session.Metadata.TryGetValue("shipmentIds", out var shipmentIdList)
                ? shipmentIdList
                : null;

            if (string.IsNullOrEmpty(shipmentIdsStr))
            {
                _logger.Warn("[Stripe][Webhook] shipmentIds not found in metadata.");
                return;
            }

            var shipmentIds = shipmentIdsStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();

            if (!shipmentIds.Any())
            {
                _logger.Warn("[Stripe][Webhook] No valid shipmentIds found.");
                return;
            }

            await _transactionService.HandleSuccessfulShipmentPaymentAsync(shipmentIds);
            _logger.Success($"[Stripe][Webhook] Đã cập nhật trạng thái PROCESSING cho các shipment: {shipmentIdsStr}");
        }
        catch (Exception ex)
        {
            _logger.Error("[Stripe][Webhook] Unhandled exception while handling shipment payment: " + ex.Message);
        }
    }

    /// <summary>
    ///     Xử lý khi thanh toán thành công từ Stripe webhook.
    /// </summary>
    private async Task HandleSuccessfulPaymentForOrder(Guid orderId, string sessionId,
        IDictionary<string, string> metadata)
    {
        await _transactionService.HandleSuccessfulPaymentAsync(sessionId, orderId.ToString());
        _logger.Success($"[Stripe][Webhook] Thanh toán thành công cho orderId: {orderId}, sessionId: {sessionId}");

        if (metadata != null &&
            metadata.TryGetValue("couponId", out var couponId) &&
            !string.IsNullOrWhiteSpace(couponId))
        {
            _logger.Info($"[Stripe][Webhook] Cleanup Stripe coupon: {couponId}");
            await _stripeService.CleanupStripeCoupon(couponId);
            _logger.Success($"[Stripe][Webhook] Đã xóa coupon Stripe: {couponId}");
        }
    }

    #endregion

    /// <summary>
    ///     Hủy thanh toán đơn hàng theo yêu cầu chủ động của user.
    /// </summary>
    [Authorize]
    [HttpPost("cancel-payment")]
    public async Task<IActionResult> CancelPayment([FromBody] CancelPaymentRequestDto request)
    {
        _logger.Info(
            $"[Stripe][CancelPayment] Yêu cầu hủy thanh toán cho order/group: {request.OrderId} / {request.CheckoutGroupId}");
        try
        {
            // Nếu truyền vào groupId thì hủy cả nhóm, còn truyền orderId thì hủy đơn lẻ
            if (request.CheckoutGroupId.HasValue && request.CheckoutGroupId.Value != Guid.Empty)
            {
                await _orderService.CancelGroupOrderPaymentAsync(request.CheckoutGroupId.Value);
                return Ok(ApiResult<object>.Success(null, "200", "Đã hủy thanh toán cho nhóm đơn hàng."));
            }
            else if (request.OrderId.HasValue && request.OrderId.Value != Guid.Empty)
            {
                await _orderService.CancelOrderPaymentAsync(request.OrderId.Value);
                return Ok(ApiResult<object>.Success(null, "200", "Đã hủy thanh toán cho đơn hàng."));
            }
            else
            {
                return BadRequest(ApiResult<object>.Failure("Thông tin request không hợp lệ hoặc thiếu thông tin orderId/checkoutGroupId."));
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[Stripe][CancelPayment] Lỗi: {ex.Message}");
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}