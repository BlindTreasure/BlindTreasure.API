using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces;

public interface ITransactionService
{
    Task<List<Transaction>> GetMyTransactionsAsync();
    Task<Transaction?> GetTransactionByIdAsync(Guid transactionId);
    Task<List<Transaction>> GetTransactionsByOrderIdAsync(Guid orderId);
    Task HandleFailedPaymentAsync(string sessionId);
    Task HandlePaymentIntentCreatedAsync(string paymentIntentId, string sessionId, string? couponId);
    Task HandleSuccessfulPaymentAsync(string sessionId, string orderId);
    Task HandleSuccessfulShipmentPaymentAsync(IEnumerable<Guid> shipmentIds);
}