using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.Entities;
using Stripe;

namespace BlindTreasure.Application.Interfaces;

public interface IStripeService
{
    Task CleanupStripeCoupon(string couponId);
    Task<string> CreateCheckoutSession(Guid orderId, bool isRenew = false);
    Task<List<OrderPaymentInfo>> CreateCheckoutSessionsForOrders(List<Guid> orderIds);
    Task<GroupPaymentSession> CreateGeneralCheckoutSessionForOrders(List<Guid> orderIds);
    Task<string> CreateShipmentCheckoutSessionAsync(List<Shipment> shipments, Guid userId, int totalShippingFee);
    Task<GroupPaymentSession> DisableStripeGroupPaymentSessionAsync(Guid checkoutGroupId, List<Order> orders);
    Task DisableStripeOrderPaymentSessionAsync(Guid orderId);
    Task<string> GenerateExpressLoginLink();
    Task<string> GenerateSellerOnboardingLinkAsync(Guid sellerId);
    Task<string> GetOrCreateGroupPaymentLink(Guid checkoutGroupId);
    Task<bool> IsSellerStripeAccountVerifiedAsync(string sellerStripeAccountId);

    Task<Transfer> PayoutToSellerAsync(Guid payoutId, string sellerStripeAccountId, decimal amount,
        string currency = "usd",
        string description = "Payout to seller");

    Task<Refund> RefundOrderAsync(Guid orderId);
    Task<TransferReversal> ReversePayoutAsync(string transferId);
}