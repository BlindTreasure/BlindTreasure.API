using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
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

    public async Task<string> CreateCheckoutSession(Guid orderId, bool isRenew = false)
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
        if (finalAmount < 0) finalAmount = 0m;

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

        // 6. Xây dựng line-items
        var lineItems = new List<SessionLineItemOptions>();

        // 6.1: Mỗi OrderDetail thành một line item
        foreach (var od in order.OrderDetails)
        {
            var name = od.ProductId.HasValue
                ? od.Product!.Name
                : od.BlindBox!.Name;

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
                    UnitAmount = (long)od.UnitPrice
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
                        Name = "Tổng phí vận chuyển",
                        Description = shipmentDesc
                    },
                    UnitAmount = (long)totalShipping
                },
                Quantity = 1
            });

        // 6.3: Tổng khuyến mãi (âm)
        if (totalDiscount > 0)
            lineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "vnd",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "Tổng khuyến mãi",
                        Description = $"Giảm: {totalDiscount:N0}đ"
                    },
                    UnitAmount = -(long)totalDiscount
                },
                Quantity = 1
            });

        // 7. Tạo session Stripe
        var options = new SessionCreateOptions
        {
            CustomerEmail = user.Email,
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "payment",
            LineItems = lineItems,
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.Id.ToString(),
                ["totalGoods"] = totalGoods.ToString(),
                ["totalShipping"] = totalShipping.ToString(),
                ["totalDiscount"] = totalDiscount.ToString(),
                ["finalAmount"] = finalAmount.ToString()
            },
            SuccessUrl = $"{_successRedirectUrl}?order_id={order.Id}&status=success",
            CancelUrl = $"{_failRedirectUrl}?order_id={order.Id}&status=pending",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["orderStatus"] = order.Status,
                    ["itemCount"] = order.OrderDetails.Count.ToString(),
                    ["currency"] = "vnd",
                    ["shipDesc"] = shipmentDesc
                }
            }
        };

        var service = new SessionService(_stripeClient);
        var session = await service.CreateAsync(options);

        // 8. Ghi vào Payment & Transaction
        await UpsertPaymentAndTransactionForOrder(order, session.Id, userId, isRenew, finalAmount);
        await _unitOfWork.SaveChangesAsync();

        return session.Url;
    }

    private async Task UpsertPaymentAndTransactionForOrder(
        Order order,
        string sessionId,
        Guid userId,
        bool isRenew,
        decimal netAmount)
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
                PaymentIntentId = "", // this field is nowhere ???
                PaidAt = now,
                RefundedAmount = 0,
                CreatedAt = now,
                CreatedBy = userId,
                Transactions = new List<Transaction>()
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