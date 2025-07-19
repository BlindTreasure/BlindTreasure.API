using BlindTreasure.Domain.Entities;
using Stripe;

namespace BlindTreasure.Application.Interfaces;

public interface IStripeService
{
    Task<string> CreateCheckoutSession(Guid orderId, bool isRenew = false);
    Task<string> CreateShipmentCheckoutSessionAsync(List<Shipment> shipments, Guid userId, int totalShippingFee);
    Task<string> GenerateExpressLoginLink();
    Task<string> GenerateSellerOnboardingLinkAsync(Guid sellerId, string redirectUrl);
    Task<bool> IsSellerStripeAccountVerifiedAsync(string sellerStripeAccountId);

    Task<Transfer> PayoutToSellerAsync(string sellerStripeAccountId, decimal amount, string currency = "usd",
        string description = "Payout to seller");

    Task<Refund> RefundPaymentAsync(string paymentIntentId, decimal amount);
    Task<TransferReversal> ReversePayoutAsync(string transferId);
}