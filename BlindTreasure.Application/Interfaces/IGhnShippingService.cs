using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces;

public interface IGhnShippingService
{
    Task<List<ProvinceDto>?> GetProvincesAsync();
    Task<List<DistrictDto>?> GetDistrictsAsync(int provinceId);
    Task<List<WardDto>?> GetWardsAsync(int districtId);
    Task<List<ServiceDTO>?> GetAvailableServicesAsync(int fromDistrict, int toDistrict);
    Task<CalculateShippingFeeResponse?> CalculateFeeAsync(CalculateShippingFeeRequest request);
    Task<GhnPreviewResponse?> PreviewOrderAsync(GhnOrderRequest req);
    Task<GhnCreateResponse?> CreateOrderAsync(GhnOrderRequest req);
}