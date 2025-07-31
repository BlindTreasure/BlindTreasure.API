using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface ISellerStatisticsService
    {
        Task<SellerSalesStatisticsDto> GetSalesStatisticsAsync(Guid? sellerId = null, DateTime? from = null, DateTime? to = null);
        Task<SellerStatisticsDto> GetStatisticsAsync(Guid sellerId, SellerStatisticsRequestDto req, CancellationToken ct = default);
    }
}
