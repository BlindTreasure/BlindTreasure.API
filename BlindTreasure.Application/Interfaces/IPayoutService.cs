using BlindTreasure.Domain.DTOs.PayoutDTOs;
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
        Task<PayoutCalculationResultDto> GetUpcomingPayoutForCurrentSellerAsync();
        Task<Payout?> GetEligiblePayoutForSellerAsync(Guid sellerId);
        Task<PayoutDetailResponseDto?> GetPayoutDetailByIdAsync(Guid payoutId);
        Task<List<PayoutListResponseDto>> GetSellerPayoutsForPeriodAsync(PayoutCalculationRequestDto req);
        Task<bool> ProcessSellerPayoutAsync(Guid sellerId);
        Task<bool> RequestPayoutAsync(Guid sellerId);
        Task<MemoryStream> ExportPayoutsByPeriodAsync(DateTime? fromDate, DateTime? toDate);
        Task<MemoryStream> ExportLatestPayoutProofAsync();
    }
}
