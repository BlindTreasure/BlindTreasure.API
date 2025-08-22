using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface IPayoutService
    {
        Task AddCompletedOrderToPayoutAsync(Order order, CancellationToken? ct = null);
        Task<bool> ProcessSellerPayoutAsync(Guid sellerId);
    }
}
