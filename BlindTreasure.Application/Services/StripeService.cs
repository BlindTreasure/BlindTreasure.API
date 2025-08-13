using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System.Text;

namespace BlindTreasure.Application.Services;

public class StripeService : IStripeService
{
    private readonly IClaimsService _claimsService;
    private readonly IConfiguration _configuration;
    private readonly string _failRedirectUrl;
    private readonly IStripeClient _stripeClient;
    private readonly string _successRedirectUrl;
    private readonly IUnitOfWork _unitOfWork;

    public StripeService(IUnitOfWork unitOfWork, IStripeClient stripeClient,
        IClaimsService claimsService, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _stripeClient = stripeClient;
        _claimsService = claimsService;
        _configuration = configuration;

        _successRedirectUrl = _configuration["STRIPE:SuccessRedirectUrl"] ?? "http://localhost:4040/thankyou";
        _failRedirectUrl = _configuration["STRIPE:FailRedirectUrl"] ?? "http://localhost:4040/fail";
    }

    public async Task<string> GetOrCreateGroupPaymentLink(Guid checkoutGroupId)
    {
        var orders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.CheckoutGroupId == checkoutGroupId && !o.IsDeleted)
            .ToListAsync();

        if (!orders.Any())
            throw ErrorHelper.BadRequest("Không tìm thấy đơn hàng hợp lệ trong nhóm.");

        var groupSession = await _unitOfWork.GroupPaymentSessions
            .FirstOrDefaultAsync(s => s.CheckoutGroupId == checkoutGroupId && !s.IsCompleted);

        if (groupSession != null && groupSession.ExpiresAt < DateTime.UtcNow)
            // Session still valid
            return groupSession.PaymentUrl;

        // If not found or expired, call the session creation method
        return await CreateGeneralCheckoutSessionForOrders(orders.Select(o => o.Id).ToList());
    }

    public async Task<string> GenerateExpressLoginLink()
    {
        var userId = _claimsService.CurrentUserId; // chỗ này là lấy user id của seller là người đang login
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(user => user.UserId == userId) ??
                     throw ErrorHelper.Forbidden("Seller is not existing");
        // Create an instance of the LoginLinkService
        var loginLinkService = new AccountLoginLinkService();

        // Create the login link for the connected account
        // Optionally, you can provide additional options (like redirect URL) if needed.
        if (string.IsNullOrEmpty(seller.StripeAccountId))
            throw ErrorHelper.BadRequest("Seller chưa có Stripe account. Vui lòng tạo trước khi đăng nhập.");
        var loginLink = await loginLinkService.CreateAsync(seller.StripeAccountId);
        return loginLink.Url;
    }

    public async Task<string> CreateGeneralCheckoutSessionForOrders(List<Guid> orderIds)
    {
        var userId = _claimsService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId)
                   ?? throw ErrorHelper.NotFound("User không tồn tại.");

        var orders = await _unitOfWork.Orders.GetQueryable()
            .Where(o => orderIds.Contains(o.Id) && o.UserId == userId && !o.IsDeleted)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.BlindBox)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Shipments)
            .Include(o => o.OrderSellerPromotions).ThenInclude(p => p.Promotion)
            .ToListAsync();

        if (!orders.Any())
            throw ErrorHelper.BadRequest("Không tìm thấy đơn hàng hợp lệ để thanh toán.");

        var lineItems = new List<SessionLineItemOptions>();
        decimal totalGoods = 0, totalShipping = 0, totalDiscount = 0;

        foreach (var order in orders)
        {
            totalGoods += order.OrderDetails.Sum(od => od.TotalPrice);
            totalShipping += order.TotalShippingFee ?? 0m;
            totalDiscount += order.OrderSellerPromotions.Sum(p => p.DiscountAmount);

            foreach (var od in order.OrderDetails)
            {
                var name = od.ProductId.HasValue ? od.Product!.Name : od.BlindBox!.Name;
                var unitPrice = Math.Max(1, (long)Math.Round(od.UnitPrice));
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "vnd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = name,
                            Description = $"Order: {order.Id} | Qty: {od.Quantity}, Tổng: {od.TotalPrice:N0}đ"
                        },
                        UnitAmount = unitPrice
                    },
                    Quantity = od.Quantity
                });
            }
        }

        if (totalShipping > 0)
            lineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "vnd",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "Phí vận chuyển",
                        Description = $"Tổng phí vận chuyển cho {orders.Count} đơn"
                    },
                    UnitAmount = (long)Math.Round(totalShipping)
                },
                Quantity = 1
            });

        string? couponId = null;
        if (totalDiscount > 0)
            // Tạo coupon cho toàn bộ discount
            couponId = await CreateStripeCouponForOrder(orderIds.First(), totalDiscount);

        var finalAmount = totalGoods + totalShipping - totalDiscount;
        if (finalAmount < 1) finalAmount = 1m;

        var options = new SessionCreateOptions
        {
            CustomerEmail = user.Email,
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "payment",
            LineItems = lineItems,
            Discounts = !string.IsNullOrEmpty(couponId)
                ? new List<SessionDiscountOptions> { new() { Coupon = couponId } }
                : null,
            Metadata = new Dictionary<string, string>
            {
                ["orderIds"] = string.Join(",", orderIds),
                ["userId"] = userId.ToString(),
                ["totalGoods"] = totalGoods.ToString("F2"),
                ["totalShipping"] = totalShipping.ToString("F2"),
                ["totalDiscount"] = totalDiscount.ToString("F2"),
                ["finalAmount"] = finalAmount.ToString("F2"),
                ["couponId"] = couponId ?? "",
                ["isGeneralPayment"] = "true",
                ["checkoutGroupId"] = orders.First().CheckoutGroupId.ToString()
            },
            SuccessUrl = $"{_successRedirectUrl}?checkout_group={orders.First().CheckoutGroupId}&status=success",
            CancelUrl = $"{_failRedirectUrl}?checkout_group={orders.First().CheckoutGroupId}&status=pending",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var service = new SessionService(_stripeClient);
        var session = await service.CreateAsync(options);

        // Ghi lại transaction cho từng order
        foreach (var order in orders)
            await UpsertPaymentAndTransactionForOrder(order, session.Id, userId, false, order.FinalAmount ?? 0,
                couponId ?? null, session.PaymentIntentId);

        // Save GroupPaymentSession
        var checkoutGroupId = orders.First().CheckoutGroupId;
        var groupSession = await _unitOfWork.GroupPaymentSessions
            .FirstOrDefaultAsync(s => s.CheckoutGroupId == checkoutGroupId && !s.IsCompleted);

        if (groupSession == null)
        {
            groupSession = new GroupPaymentSession
            {
                CheckoutGroupId = checkoutGroupId,
                StripeSessionId = session.Id,
                PaymentUrl = session.Url,
                ExpiresAt = session.ExpiresAt,
                Type = PaymentType.Order,
                IsCompleted = false,
                CouponId = couponId,
                PaymentIntentId = session.PaymentIntentId
            };
            await _unitOfWork.GroupPaymentSessions.AddAsync(groupSession);
        }
        else
        {
            groupSession.StripeSessionId = session.Id;
            groupSession.PaymentUrl = session.Url;
            groupSession.ExpiresAt = session.ExpiresAt;
            groupSession.IsCompleted = false;
            await _unitOfWork.GroupPaymentSessions.Update(groupSession);
        }

        await _unitOfWork.SaveChangesAsync();

        return session.Url;
    }

    public async Task<List<OrderPaymentInfo>> CreateCheckoutSessionsForOrders(List<Guid> orderIds)
    {
        var result = new List<OrderPaymentInfo>();
        foreach (var orderId in orderIds)
        {
            var url = await CreateCheckoutSession(orderId);
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            result.Add(new OrderPaymentInfo
            {
                OrderId = orderId,
                SellerId = order.SellerId,
                SellerName = order.Seller?.CompanyName ?? "Unknown",
                PaymentUrl = url,
                FinalAmount = order.FinalAmount ?? 0
            });
        }

        return result;
    }

    public async Task<string> CreateCheckoutSession(Guid orderId, bool isRenew = false)
    {
        try
        {
            // 1. Lấy user hiện tại
            var userId = _claimsService.CurrentUserId;
            var user = await _unitOfWork.Users.GetByIdAsync(userId)
                       ?? throw ErrorHelper.NotFound("User không tồn tại.");

            // 2. Lấy order, include luôn OrderDetails, Product, BlindBox, Shipments, và OrderSellerPromotions
            var order = await _unitOfWork.Orders.GetQueryable()
                            .Where(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted)
                            .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.Product)
                            .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.BlindBox)
                            .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.Shipments)
                            .Include(o => o.OrderSellerPromotions)
                            .ThenInclude(p => p.Promotion)
                            .FirstOrDefaultAsync()
                        ?? throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");

            // 3. Kiểm tra trạng thái đơn cho checkout hoặc renew
            if (!isRenew)
            {
                if (order.Status != OrderStatus.PENDING.ToString())
                    throw ErrorHelper.BadRequest("Chỉ có thể thanh toán đơn chờ xử lý.");
            }
            else
            {
                if (order.Status != OrderStatus.CANCELLED.ToString() &&
                    order.Status != OrderStatus.EXPIRED.ToString())
                    throw ErrorHelper.BadRequest("Chỉ có thể gia hạn đơn đã hủy hoặc hết hạn.");
            }

            // 4. Tính tổng các khoản
            var totalGoods = order.OrderDetails.Sum(od => od.TotalPrice);
            var totalShipping = order.TotalShippingFee ?? 0m;
            var totalDiscount = order.OrderSellerPromotions.Sum(p => p.DiscountAmount);
            var finalAmount = totalGoods + totalShipping - totalDiscount;
            if (finalAmount < 1) finalAmount = 1m; // Stripe yêu cầu tối thiểu 1 VND

            // 5. Chuẩn bị shipmentDescriptions (nếu cần)
            var shipmentDescriptions = new List<string>();
            foreach (var od in order.OrderDetails)
            foreach (var s in od.Shipments)
                shipmentDescriptions.Add(
                    $"#{od.Id}: {s.Provider} - mã {s.OrderCode ?? "N/A"} - phí {s.TotalFee:N0}đ - trạng thái {s.Status}"
                );

            var shipmentDesc = shipmentDescriptions.Any()
                ? string.Join(" | ", shipmentDescriptions)
                : "Không có thông tin giao hàng.";

            // 6. Xây dựng line-items (giữ nguyên cách hiển thị chi tiết)
            var lineItems = new List<SessionLineItemOptions>();

            // 6.1: Mỗi OrderDetail thành một line item
            foreach (var od in order.OrderDetails)
            {
                var name = od.ProductId.HasValue
                    ? od.Product!.Name
                    : od.BlindBox!.Name;

                // Đảm bảo UnitPrice không âm
                var unitPrice = Math.Max(1, (long)Math.Round(od.UnitPrice));

                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "vnd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = name,
                            Description = $"Qty: {od.Quantity}, Tổng: {od.TotalPrice:N0}đ"
                        },
                        UnitAmount = unitPrice
                    },
                    Quantity = od.Quantity
                });
            }

            // 6.2: Tổng phí vận chuyển
            if (totalShipping > 0)
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "vnd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Phí vận chuyển",
                            Description = shipmentDesc.Length > 200
                                ? shipmentDesc.Substring(0, 197) + "..."
                                : shipmentDesc
                        },
                        UnitAmount = (long)Math.Round(totalShipping)
                    },
                    Quantity = 1
                });

            // 6.3: Tạo Stripe Coupon cho discount (thay vì line item âm)
            string? couponId = null;
            if (totalDiscount > 0) couponId = await CreateStripeCouponForOrder(order.Id, totalDiscount);

            // 7. Tạo session Stripe
            var options = new SessionCreateOptions
            {
                CustomerEmail = user.Email,
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                LineItems = lineItems,

                // Áp dụng coupon nếu có discount
                Discounts = !string.IsNullOrEmpty(couponId)
                    ? new List<SessionDiscountOptions>
                    {
                        new()
                        {
                            Coupon = couponId
                        }
                    }
                    : null,

                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = order.Id.ToString(),
                    ["userId"] = userId.ToString(),
                    ["totalGoods"] = totalGoods.ToString("F2"),
                    ["totalShipping"] = totalShipping.ToString("F2"),
                    ["totalDiscount"] = totalDiscount.ToString("F2"),
                    ["finalAmount"] = finalAmount.ToString("F2"),
                    ["isRenew"] = isRenew.ToString(),
                    ["couponId"] = couponId ?? ""
                },
                SuccessUrl = $"{_successRedirectUrl}?order_id={order.Id}&status=success",
                CancelUrl = $"{_failRedirectUrl}?order_id={order.Id}&status=pending",
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["orderStatus"] = order.Status,
                        ["itemCount"] = order.OrderDetails.Count.ToString(),
                        ["currency"] = "vnd",
                        ["shipDesc"] = shipmentDesc.Length > 500 ? shipmentDesc.Substring(0, 497) + "..." : shipmentDesc
                    }
                }
            };

            var service = new SessionService(_stripeClient);
            var session = await service.CreateAsync(options);

            // 8. Ghi vào Payment & Transaction??nu
            await UpsertPaymentAndTransactionForOrder(order, session.Id, userId, isRenew, finalAmount, couponId ?? null,
                session.PaymentIntentId);
            await _unitOfWork.SaveChangesAsync();

            return session.Url;
        }
        catch (StripeException stripeEx)
        {
            throw ErrorHelper.BadRequest($"Lỗi Stripe: {stripeEx.Message}");
        }
        catch (Exception ex)
        {
            throw ErrorHelper.BadRequest($"Lỗi tạo phiên thanh toán: {ex.Message}");
        }
    }

    /// <summary>
    /// Tạo Stripe Coupon động cho đơn hàng
    /// </summary>
    private async Task<string> CreateStripeCouponForOrder(Guid orderId, decimal discountAmount)
    {
        try
        {
            var couponService = new CouponService(_stripeClient);

            // Tạo coupon ID duy nhất cho đơn hàng (rút gọn để tránh quá dài)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var shortOrderId = orderId.ToString("N")[..8]; // Lấy 8 ký tự đầu của GUID
            var couponId = $"ord-{shortOrderId}-{timestamp}";

            var couponOptions = new CouponCreateOptions
            {
                Id = couponId,
                Name = $"Discount-{shortOrderId}", // Rút gọn name để <= 40 ký tự
                Currency = "vnd",
                AmountOff = (long)Math.Round(discountAmount), // Số tiền giảm cố định (VND)
                Duration = "once", // Chỉ sử dụng một lần
                MaxRedemptions = 1, // Chỉ có thể sử dụng 1 lần
                RedeemBy = DateTime.UtcNow.AddHours(25),
                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = orderId.ToString(),
                    ["discountAmount"] = discountAmount.ToString("F2"),
                    ["createdAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["fullOrderId"] = orderId.ToString("N") // Lưu full order ID trong metadata
                }
            };

            var coupon = await couponService.CreateAsync(couponOptions);
            return coupon.Id;
        }
        catch (StripeException ex)
        {
            // Nếu tạo coupon thất bại, log lỗi nhưng không làm crash process
            // Có thể fallback về cách tính toán trực tiếp
            throw new InvalidOperationException($"Không thể tạo coupon giảm giá: {ex.Message}");
        }
    }

    /// <summary>
    /// Vô hiệu hóa session thanh toán Stripe (hủy PaymentIntent và xóa coupon nếu còn hiệu lực)
    /// </summary>
    public async Task DisableStripeGroupPaymentSessionAsync(Guid checkoutGroupId)
    {
        var groupSession = await _unitOfWork.GroupPaymentSessions
            .FirstOrDefaultAsync(s => s.CheckoutGroupId == checkoutGroupId && !s.IsCompleted);

        if (groupSession == null)
            return;

        // Hủy PaymentIntent nếu còn hiệu lực
        if (!string.IsNullOrWhiteSpace(groupSession.PaymentIntentId))
            try
            {
                var paymentIntentService = new PaymentIntentService(_stripeClient);
                var paymentIntent = await paymentIntentService.GetAsync(groupSession.PaymentIntentId);
                if (paymentIntent != null && paymentIntent.Status == "requires_payment_method")
                    await paymentIntentService.CancelAsync(groupSession.PaymentIntentId);
            }
            catch (StripeException)
            {
                // Log error nhưng không throw
            }

        // Xóa coupon nếu có
        if (!string.IsNullOrWhiteSpace(groupSession.CouponId)) await CleanupStripeCoupon(groupSession.CouponId);
    }

    /// <summary>
    /// Vô hiệu hóa session thanh toán Stripe cho đơn lẻ (hủy PaymentIntent và xóa coupon nếu còn hiệu lực)
    /// </summary>
    public async Task DisableStripeOrderPaymentSessionAsync(Guid orderId)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order?.Payment == null)
            return;

        // Hủy PaymentIntent nếu còn hiệu lực
        if (!string.IsNullOrWhiteSpace(order.Payment.PaymentIntentId))
            try
            {
                var paymentIntentService = new PaymentIntentService(_stripeClient);
                var paymentIntent = await paymentIntentService.GetAsync(order.Payment.PaymentIntentId);
                if (paymentIntent != null && paymentIntent.Status == "requires_payment_method")
                    await paymentIntentService.CancelAsync(order.Payment.PaymentIntentId);
            }
            catch (StripeException)
            {
                // Log error nhưng không throw
            }

        // Xóa coupon nếu có
        if (!string.IsNullOrWhiteSpace(order.Payment.CouponId)) await CleanupStripeCoupon(order.Payment.CouponId);
    }

    /// <summary>
    /// Xóa coupon sau khi sử dụng (gọi trong webhook hoặc sau khi thanh toán thành công)
    /// </summary>
    public async Task CleanupStripeCoupon(string couponId)
    {
        try
        {
            if (string.IsNullOrEmpty(couponId)) return;

            var couponService = new CouponService(_stripeClient);
            await couponService.DeleteAsync(couponId);
        }
        catch (StripeException)
        {
            // Log error nhưng không throw - việc cleanup không quan trọng bằng main flow
        }
    }

    private async Task UpsertPaymentAndTransactionForOrder(
        Order order,
        string sessionId,
        Guid userId,
        bool isRenew,
        decimal netAmount,
        string? couponId,
        string? paymentIntentId)
    {
        var now = DateTime.UtcNow;
        var type = isRenew ? "Renew" : "Checkout";

        if (order.Payment == null)
        {
            var payment = new Payment
            {
                Order = order,
                Amount = order.TotalAmount + (order.TotalShippingFee ?? 0m),
                DiscountRate = 0,
                NetAmount = netAmount,
                Method = "Stripe",
                Status = PaymentStatus.Pending,
                PaymentIntentId = paymentIntentId,
                PaidAt = now,
                RefundedAmount = 0,
                CreatedAt = now,
                CreatedBy = userId,
                Transactions = new List<Transaction>(),
                CouponId = couponId
            };

            payment.Transactions.Add(new Transaction
            {
                Payment = payment,
                Type = type,
                Amount = netAmount,
                Currency = "vnd",
                Status = "PENDING",
                OccurredAt = now,
                ExternalRef = sessionId,
                CreatedAt = now,
                CreatedBy = userId
            });

            order.Payment = payment;
            await _unitOfWork.Orders.Update(order);
        }
        else
        {
            var tx = new Transaction
            {
                Payment = order.Payment,
                Type = type,
                Amount = netAmount,
                Currency = "vnd",
                Status = "PENDING",
                OccurredAt = now,
                ExternalRef = sessionId,
                CreatedAt = now,
                CreatedBy = userId
            };
            order.Payment.Transactions.Add(tx);
            await _unitOfWork.Payments.Update(order.Payment);
        }
    }


    // 1. Chuyển tiền payout cho seller (Stripe Connect)
    public async Task<Transfer> PayoutToSellerAsync(string sellerStripeAccountId, decimal amount,
        string currency = "usd", string description = "Payout to seller")
    {
        var userId = _claimsService.CurrentUserId; // chỗ này là lấy user id của seller là người đang login
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(user => user.Id == userId) ??
                   throw ErrorHelper.Forbidden("User is not existing");

        var transferService = new TransferService(_stripeClient);
        var transferOptions = new TransferCreateOptions
        {
            Amount = (long)amount, // Stripe expects smallest unit (vnd: xu)
            Currency = currency,
            Destination = sellerStripeAccountId,
            Description = description + $" - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}" + "made by user:" + user.FullName
        };

        try
        {
            var transfer = await transferService.CreateAsync(transferOptions);
            if (transfer == null)
                throw ErrorHelper.Internal("Stripe payout failed.");
            // TODO: Lưu transaction payout vào DB nếu cần

            var transaction = new Transaction
            {
                Type = TransactionType.Payout.ToString(),
                Amount = amount,
                Currency = currency,
                ExternalRef = transfer.Id,
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();

            return transfer;
        }
        catch (StripeException ex)
        {
            throw ErrorHelper.BadRequest($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    public async Task<string> CreateShipmentCheckoutSessionAsync(List<Shipment> shipments, Guid userId,
        int totalShippingFee)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            throw ErrorHelper.NotFound("User không tồn tại.");

        var lineItems = new List<SessionLineItemOptions>
        {
            new()
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "vnd",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "Phí vận chuyển nhiều đơn GHN",
                        Description = string.Join(" | ",
                            shipments.Select(s => $"Mã vận đơn: {s.OrderCode}, Phí: {s.TotalFee:N0} VND"))
                    },
                    UnitAmount = totalShippingFee
                },
                Quantity = 1
            }
        };

        var options = new SessionCreateOptions
        {
            Metadata = new Dictionary<string, string>
            {
                { "shipmentIds", string.Join(",", shipments.Select(s => s.Id)) },
                { "userId", userId.ToString() },
                { "IsShipmenRequest", true.ToString() }
            },
            CustomerEmail = user.Email,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = lineItems,
            Mode = "payment",
            SuccessUrl = $"{_successRedirectUrl}?status=success&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_failRedirectUrl}?status=failed&session_id={{CHECKOUT_SESSION_ID}}",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        var service = new SessionService(_stripeClient);
        var session = await service.CreateAsync(options);

        return session.Url;
    }

    // 2. Hoàn lại tiền cho khách (refund)
    public async Task<Refund> RefundPaymentAsync(string paymentIntentId, decimal amount)
    {
        var refundService = new RefundService(_stripeClient);
        var refundOptions = new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = (long)amount // Stripe expects smallest unit
        };

        try
        {
            var refund = await refundService.CreateAsync(refundOptions);
            if (refund == null)
                throw ErrorHelper.Internal("Stripe refund failed.");
            // TODO: Lưu transaction refund vào DB nếu cần
            var transaction = new Transaction
            {
                Type = TransactionType.Refund.ToString(),
                Amount = amount,
                Currency = refund.Currency,
                ExternalRef = refund.Id,
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _claimsService.CurrentUserId
            };
            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();
            return refund;
        }
        catch (StripeException ex)
        {
            throw ErrorHelper.BadRequest($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    // 3. Tạo onboarding link cho seller (Stripe Express)
    public async Task<string> GenerateSellerOnboardingLinkAsync(Guid sellerId, string redirectUrl)
    {
        var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId);
        if (seller == null)
            throw ErrorHelper.NotFound("Seller không tồn tại.");

        // Nếu chưa có StripeAccountId thì tạo mới
        if (string.IsNullOrEmpty(seller.StripeAccountId))
        {
            var acOptions = new AccountCreateOptions
            {
                Country = "VN",
                Type = "express",
                Email = seller.User?.Email,
                Capabilities = new AccountCapabilitiesOptions
                {
                    Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
                },
                BusinessType = "individual",
                BusinessProfile = new AccountBusinessProfileOptions
                {
                    Name = seller.CompanyName,
                    Mcc = "5945", // Toys, Hobby, and Game Shops
                    ProductDescription = seller.CompanyProductDescription,
                    SupportEmail = seller.User?.Email,
                    SupportPhone = seller.CompanyPhone,
                    SupportAddress = new AddressOptions
                    {
                        Line1 = seller.CompanyAddress,
                        City = seller.CompanyProvinceName,
                        State = seller.CompanyDistrictName,
                        PostalCode = "700000", // Mã bưu điện tạm thời
                        Country = "VN"
                    }
                },
                TosAcceptance = new AccountTosAcceptanceOptions
                {
                    ServiceAgreement = "recipient"
                }
            };
            var accountService = new AccountService(_stripeClient);
            var account = await accountService.CreateAsync(acOptions);

            seller.StripeAccountId = account.Id;
            await _unitOfWork.Sellers.Update(seller);
            await _unitOfWork.SaveChangesAsync();
        }

        var linkOptions = new AccountLinkCreateOptions
        {
            Account = seller.StripeAccountId,
            RefreshUrl = $"{redirectUrl}/profile",
            ReturnUrl = $"{redirectUrl}/profile",
            Type = "account_onboarding"
        };

        var linkService = new AccountLinkService(_stripeClient);
        var accountLink = await linkService.CreateAsync(linkOptions);

        return accountLink.Url;
    }

    // 4. Kiểm tra seller đã xác minh Stripe account chưa (đủ điều kiện nhận tiền)
    public async Task<bool> IsSellerStripeAccountVerifiedAsync(string sellerStripeAccountId)
    {
        var accountService = new AccountService(_stripeClient);
        var account = await accountService.GetAsync(sellerStripeAccountId);
        return account.ChargesEnabled && account.PayoutsEnabled;
    }

    // 5. Đảo ngược payout (reversal) nếu cần
    public async Task<TransferReversal> ReversePayoutAsync(string transferId)
    {
        var reversalService = new TransferReversalService(_stripeClient);
        var options = new TransferReversalCreateOptions();
        return await reversalService.CreateAsync(transferId, options);
    }
}