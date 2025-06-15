using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface ITransactionService
    {
        Task<List<Transaction>> GetMyTransactionsAsync();
        Task<Transaction?> GetTransactionByIdAsync(Guid transactionId);
        Task<List<Transaction>> GetTransactionsByOrderIdAsync(Guid orderId);
        Task HandleFailedPaymentAsync(string sessionId);
        Task HandlePaymentIntentCreatedAsync(string paymentIntentId, string sessionId);
        Task HandleSuccessfulPaymentAsync(string sessionId, string orderId);
    }
}
