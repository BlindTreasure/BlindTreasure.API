using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace BlindTreasure.Application.Services;

public class StripeService : IStripeService
{
    private readonly IClaimsService _claimsService;
    private readonly IStripeClient _stripeClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string succesRedirectUrl = "http://localhost:4040/thankyou";
    private readonly string failRedirectUrl = "http://localhost:4040/fail";

    public StripeService(IUnitOfWork unitOfWork, IStripeClient stripeClient,
        IClaimsService claimsService)
    {
        _unitOfWork = unitOfWork;
        _stripeClient = stripeClient;
        _claimsService = claimsService;
    }

    public async Task<string> GenerateExpressLoginLink()
    {
        var userId = _claimsService.CurrentUserId; // chỗ này là lấy user id của seller là người đang login
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(user => user.Id == userId) ??
                     throw ErrorHelper.Forbidden("Seller is not existing");
        // Create an instance of the LoginLinkService
        var loginLinkService = new AccountLoginLinkService();

        // Create the login link for the connected account
        // Optionally, you can provide additional options (like redirect URL) if needed.
        var loginLink = await loginLinkService.CreateAsync(seller.Id.ToString());
        return loginLink.Url;
    }


    public async Task<string> CreateCheckoutSession(Guid orderId, bool isRenew = false)
    {
        // Lấy user hiện tại
        var userId = _claimsService.CurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            throw ErrorHelper.NotFound("User không tồn tại.");

        // Lấy order và kiểm tra quyền sở hữu
        var order = await _unitOfWork.Orders.GetQueryable()
            .Where(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.BlindBox)
            .FirstOrDefaultAsync();

        if (order == null)
            throw ErrorHelper.NotFound("Đơn hàng không tồn tại.");

        if (!isRenew)
        {
            if (order.Status != OrderStatus.PENDING.ToString())
                throw ErrorHelper.BadRequest("Chỉ có thể thanh toán đơn hàng ở trạng thái chờ xử lý.");
        }
        else
        {
            if (order.Status != OrderStatus.EXPIRED.ToString() && order.Status != OrderStatus.COMPLETED.ToString())
                throw ErrorHelper.BadRequest("Chỉ có thể gia hạn đơn hàng đã hoàn thành hoặc hết hạn.");
        }


        // Chuẩn bị line items cho Stripe
        var lineItems = new List<SessionLineItemOptions>();
        foreach (var item in order.OrderDetails)
        {
            string name;
            decimal unitPrice;
            if (item.ProductId.HasValue && item.Product != null)
            {
                name = item.Product.Name;
                unitPrice = item.Product.Price;
            }
            else if (item.BlindBoxId.HasValue && item.BlindBox != null)
            {
                name = item.BlindBox.Name;
                unitPrice = item.BlindBox.Price;
            }
            else
            {
                throw ErrorHelper.BadRequest("Sản phẩm hoặc BlindBox trong đơn hàng không hợp lệ.");
            }

            lineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "vnd",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Product/Blindbox Name: {name} , Order Detail id {item.Id} belongs to Order {order.Id} paid by {user.Email}",
                        Description = $"Product/BlindBox Name: {name}\n" +
                                                  $"Quantity: {item.Quantity} / Total: {item.TotalPrice} \n" +
                                                  $"Price: {unitPrice} VND\n" +
                                                  $"Time: {item.CreatedAt}" ,
                    },
                    UnitAmount = (long)(unitPrice * 100), // Stripe expects amount in cents
                },
                Quantity = item.Quantity
            });
        }

        var options = new SessionCreateOptions
        {
            CustomerEmail = user.Email,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = lineItems,
            Mode = "payment",
            SuccessUrl = $"{succesRedirectUrl}/payment?status=success&session_id={{CHECKOUT_SESSION_ID}}&order_id={orderId}",
            CancelUrl = $"{failRedirectUrl}/payment?status=cancel&session_id={{CHECKOUT_SESSION_ID}}&order_id={orderId}",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "orderId", orderId.ToString() },
                    { "userId", userId.ToString() },
                    { "createdAt", DateTime.UtcNow.ToString("o") },
                    { "email", user.Email },
                    { "orderStatus", order.Status },
                    { "itemCount", order.OrderDetails.Count.ToString() },
                    { "totalAmount", order.TotalAmount.ToString() },
                    { "currency", "vnd" },
                    { "isRenew", isRenew.ToString() }
                }
            }
        };

        var service = new SessionService(_stripeClient);
        Session session = await service.CreateAsync(options);

        // Có thể lưu session.Id vào DB để đối soát khi webhook trả về

        // Tạo transaction (gắn với Payment nếu có, nếu chưa có thì tạo Payment trước)
        // Tạo Payment và Transaction, gắn vào order và payment (dùng ICollection)
        if (order.Payment == null)
        {
            var payment = new Payment
            {
                // Id sẽ được EF sinh khi SaveChanges
                Order = order,
                Amount = order.TotalAmount,
                DiscountRate = 0,
                NetAmount = order.TotalAmount,
                Method = "Stripe",
                Status = "Pending",
                TransactionId = "",
                PaidAt = DateTime.UtcNow,
                RefundedAmount = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                Transactions = new List<Transaction>()
            };

            var transaction = new Transaction
            {
                // Id sẽ được EF sinh khi SaveChanges
                Payment = payment,
                Type = isRenew ? "Renew" : "Checkout",
                Amount = order.TotalAmount,
                Currency = "vnd",
                Status = "Pending",
                OccurredAt = DateTime.UtcNow,
                ExternalRef = session.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            payment.Transactions.Add(transaction);
            order.Payment = payment;
            // Chỉ cần update order, EF sẽ tự cascade add payment và transaction
            await _unitOfWork.Orders.Update(order);
        }
        else
        {
            // Nếu đã có payment, chỉ cần add transaction
            var transaction = new Transaction
            {
                Payment = order.Payment,
                Type = isRenew ? "Renew" : "Checkout",
                Amount = order.TotalAmount,
                Currency = "vnd",
                Status = "Pending",
                OccurredAt = DateTime.UtcNow,
                ExternalRef = session.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            order.Payment.Transactions ??= new List<Transaction>();
            order.Payment.Transactions.Add(transaction);
            await _unitOfWork.Payments.Update(order.Payment);
        }

        await _unitOfWork.SaveChangesAsync();

        return session.Url;


    }

    // 1. Chuyển tiền payout cho seller (Stripe Connect)
    public async Task<Transfer> PayoutToSellerAsync(string sellerStripeAccountId, decimal amount, string currency = "usd", string description = "Payout to seller")
    {
        var userId = _claimsService.CurrentUserId; // chỗ này là lấy user id của seller là người đang login
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(user => user.Id == userId) ??
                     throw ErrorHelper.Forbidden("User is not existing");

        var transferService = new TransferService(_stripeClient);
        var transferOptions = new TransferCreateOptions
        {
            Amount = (long)(amount), // Stripe expects smallest unit (vnd: xu)
            Currency = currency,
            Destination = sellerStripeAccountId,
            Description = description + $" - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}" + "made by user:" +user.FullName,
        };

        try
        {
            var transfer = await transferService.CreateAsync(transferOptions);
            if (transfer == null)
                throw ErrorHelper.Internal("Stripe payout failed.");
            // TODO: Lưu transaction payout vào DB nếu cần

            var transaction = new Transaction
            {
                Type = "Payout",
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

    // 2. Hoàn lại tiền cho khách (refund)
    public async Task<Refund> RefundPaymentAsync(string paymentIntentId, decimal amount)
    {
        var refundService = new RefundService(_stripeClient);
        var refundOptions = new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = (long)(amount), // Stripe expects smallest unit
        };

        try
        {
            var refund = await refundService.CreateAsync(refundOptions);
            if (refund == null)
                throw ErrorHelper.Internal("Stripe refund failed.");
            // TODO: Lưu transaction refund vào DB nếu cần
            var transaction = new Transaction
            {
                Type = "Refund",
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
                    Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
                },
                BusinessType = "individual",
                BusinessProfile = new AccountBusinessProfileOptions
                {
                    Name = seller.CompanyName,
                    Mcc = "5945", // Toys, Hobby, and Game Shops
                    ProductDescription = "Marketplace for collectibles",
                    SupportEmail = seller.User?.Email
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