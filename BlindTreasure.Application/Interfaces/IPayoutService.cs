using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PayoutDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;
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
        Task<PayoutDetailResponseDto?> RequestPayoutAsync(Guid sellerId);
        Task<MemoryStream> ExportPayoutByIdAsync(Guid payoutId);
        Task<MemoryStream> ExportLatestPayoutProofAsync();
        Task<Pagination<PayoutListResponseDto>> GetPayoutsForAdminAsync(PayoutAdminQueryParameter param);
        Task<Pagination<PayoutListResponseDto>> GetPayoutsForCurrentSellerAsync(PayoutAdminQueryParameter param);
        Task<PayoutDetailResponseDto?> AdminConfirmPayoutWithProofAsync(Guid payoutId, List<IFormFile> files, Guid adminUserId);
        Task<PayoutListResponseDto?> GetEligiblePayoutDtoForSellerAsync(Guid sellerId);
    }
}
