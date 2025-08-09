using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using Stripe;

namespace BlindTreasure.Application.Interfaces;

public interface IStripeService
{
    Task CleanupStripeCoupon(string couponId);
    Task<string> CreateCheckoutSession(Guid orderId, bool isRenew = false);
    Task<List<OrderPaymentInfo>> CreateCheckoutSessionsForOrders(List<Guid> orderIds);
    Task<string> CreateGeneralCheckoutSessionForOrders(List<Guid> orderIds);
    Task<string> CreateShipmentCheckoutSessionAsync(List<Shipment> shipments, Guid userId, int totalShippingFee);
    Task<string> GenerateExpressLoginLink();
    Task<string> GenerateSellerOnboardingLinkAsync(Guid sellerId, string redirectUrl);
    Task<string> GetOrCreateGroupPaymentLink(Guid checkoutGroupId);
    Task<bool> IsSellerStripeAccountVerifiedAsync(string sellerStripeAccountId);

    Task<Transfer> PayoutToSellerAsync(string sellerStripeAccountId, decimal amount, string currency = "usd",
        string description = "Payout to seller");

    Task<Refund> RefundPaymentAsync(string paymentIntentId, decimal amount);
    Task<TransferReversal> ReversePayoutAsync(string transferId);
}