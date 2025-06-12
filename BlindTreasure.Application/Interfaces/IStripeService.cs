
namespace BlindTreasure.Application.Interfaces;

public interface IStripeService
{
    Task<string> CreateCheckoutSession(Guid orderId);
    Task<string> GenerateExpressLoginLink();
}