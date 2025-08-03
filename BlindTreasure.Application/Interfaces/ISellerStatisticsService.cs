using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces;

public interface ISellerStatisticsService
{
    Task<SellerDashboardStatisticsDto> GetDashboardStatisticsAsync(Guid sellerId, SellerStatisticsRequestDto req, CancellationToken ct = default);
}