using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services;

public class GhnShippingService : IGhnShippingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerService _logger;
    private const string TOKEN = "28729d34-5751-11f0-9b81-222185cb68c8";
    private const string SHOP_ID = "197002";
    private const string BASE_URL = "https://dev-online-gateway.ghn.vn";
    private const string PREVIEW_ORDER = "/shiip/public-api/v2/shipping-order/preview";
    private const string CREATE_ORDER = "/shiip/public-api/v2/shipping-order/create";
    private const string GET_PROVINCE = "/shiip/public-api/master-data/province";
    private const string GET_DISTRICT = "/shiip/public-api/master-data/district";
    private const string GET_WARD = "/shiip/public-api/master-data/ward";
    private const string GET_SERVICES = "/shiip/public-api/v2/shipping-order/available-services";
    private const string CALCULATE_FEE = "/shiip/public-api/v2/shipping-order/fee";


    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public GhnShippingService(
        IHttpClientFactory httpClientFactory,
        ILoggerService logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(BASE_URL);
        client.DefaultRequestHeaders.Add("Token", TOKEN);
        client.DefaultRequestHeaders.Add("ShopId", SHOP_ID);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    /// <summary>
    /// Xem thông tin trả về của đơn hàng trước khi tạo (GHN Preview) để preview và biết trước được thông tin + phí dịch vụ.
    /// </summary>
    public async Task<GhnPreviewResponse?> PreviewOrderAsync(GhnOrderRequest req)
    {
        _logger.Info("[GhnShippingService][PreviewOrderAsync] Preview GHN order request.");
        var client = CreateClient();
        var payload = JsonSerializer.Serialize(req, _jsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        _logger.Info(await content.ReadAsStringAsync());

        var resp = await client.PostAsync(PREVIEW_ORDER, content);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error($"[GhnShippingService][PreviewOrderAsync] GHN error: {resp.StatusCode} - {body}");
            var errorCheck = JsonSerializer.Deserialize<ApiResponse<object>>(body, _jsonOptions);
            if (errorCheck == null)
            {
                _logger.Error("Error message: Invalid response from GHN.");
                return null;
            }

            _logger.Error($"Code message: {errorCheck?.CodeMessage} \n" +
                          $"Message display: {errorCheck?.Message}");
            throw ErrorHelper.BadRequest($"GHN error: {errorCheck?.Message}");
        }

        var apiResp = JsonSerializer.Deserialize<ApiResponse<GhnPreviewResponse>>(body, _jsonOptions);
        if (apiResp == null || apiResp.Data == null)
        {
            _logger.Error("[GhnShippingService][PreviewOrderAsync] Invalid response from GHN.");
            throw new Exception($"GHN error: {apiResp?.Message}");
        }

        _logger.Success("[GhnShippingService][PreviewOrderAsync] Preview success.");
        return apiResp.Data;
    }

    /// <summary>
    /// Tạo đơn hàng chính thức trên GHN.
    /// </summary>
    public async Task<GhnCreateResponse?> CreateOrderAsync(GhnOrderRequest req)
    {
        _logger.Info("[GhnShippingService][CreateOrderAsync] Create GHN order request.");
        var client = CreateClient();
        var payload = JsonSerializer.Serialize(req, _jsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await client.PostAsync(CREATE_ORDER, content);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error($"[GhnShippingService][CreateOrderAsync] GHN error: {resp.StatusCode} - {body}");
            var errorCheck = JsonSerializer.Deserialize<ApiResponse<object>>(body, _jsonOptions);
            if (errorCheck == null)
            {
                _logger.Error("Error message: Invalid response from GHN.");
                return null;
            }

            _logger.Error($"Code message: {errorCheck?.CodeMessage} \n" +
                          $"Message display: {errorCheck?.Message}");
            throw ErrorHelper.BadRequest($"GHN error: {errorCheck?.Message}");
        }

        var apiResp = JsonSerializer.Deserialize<ApiResponse<GhnCreateResponse>>(body, _jsonOptions);
        if (apiResp == null || apiResp.Data == null)
        {
            _logger.Error("[GhnShippingService][CreateOrderAsync] Invalid response from GHN.");
            throw ErrorHelper.BadRequest("Invalid response from GHN.");
        }

        _logger.Success("[GhnShippingService][CreateOrderAsync] Create order success.");
        return apiResp.Data;
    }

    public async Task<List<ProvinceDto>?> GetProvincesAsync()
    {
        _logger.Info("[GhnShippingService][GetProvincesAsync] Getting provinces.");
        var client = CreateClient();
        var resp = await client.GetAsync(GET_PROVINCE);
        var body = await resp.Content.ReadAsStringAsync();

        _logger.Info(body);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error($"[GhnShippingService][GetProvincesAsync] GHN error: {resp.StatusCode} - {body}");
            throw ErrorHelper.BadRequest($"GHN error: {body}");
        }

        var apiResp = JsonSerializer.Deserialize<ApiResponse<List<ProvinceDto>>>(body);
        return apiResp?.Data;
    }

    public async Task<List<DistrictDto>?> GetDistrictsAsync(int provinceId)
    {
        _logger.Info($"[GhnShippingService][GetDistrictsAsync] Getting districts for province {provinceId}.");
        var client = CreateClient();
        var resp = await client.GetAsync($"{GET_DISTRICT}?province_id={provinceId}");
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error($"[GhnShippingService][GetDistrictsAsync] GHN error: {resp.StatusCode} - {body}");
            throw ErrorHelper.BadRequest($"GHN error: {body}");
        }

        var apiResp = JsonSerializer.Deserialize<ApiResponse<List<DistrictDto>>>(body);
        return apiResp?.Data;
    }

    public async Task<List<WardDto>?> GetWardsAsync(int districtId)
    {
        _logger.Info($"[GhnShippingService][GetWardsAsync] Getting wards for district {districtId}.");
        var client = CreateClient();
        var resp = await client.GetAsync($"{GET_WARD}?district_id={districtId}");
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error($"[GhnShippingService][GetWardsAsync] GHN error: {resp.StatusCode} - {body}");
            throw ErrorHelper.BadRequest($"GHN error: {body}");
        }

        var apiResp = JsonSerializer.Deserialize<ApiResponse<List<WardDto>>>(body);
        return apiResp?.Data;
    }

    public async Task<List<ServiceDTO>?> GetAvailableServicesAsync(int fromDistrict, int toDistrict)
    {
        _logger.Info(
            $"[GhnShippingService][GetAvailableServicesAsync] Getting services from {fromDistrict} to {toDistrict}.");
        var client = CreateClient();
        var resp = await client.GetAsync(
            $"{GET_SERVICES}?shop_id={SHOP_ID}&from_district={fromDistrict}&to_district={toDistrict}");
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error($"[GhnShippingService][GetAvailableServicesAsync] GHN error: {resp.StatusCode} - {body}");
            var errorCheck = JsonSerializer.Deserialize<ApiResponse<object>>(body, _jsonOptions);
            if (errorCheck == null)
            {
                _logger.Error("Error message: Invalid response from GHN.");
                return null;
            }

            _logger.Error($"Error message: {errorCheck?.CodeMessage}");
            throw ErrorHelper.BadRequest($"GHN error: {errorCheck?.Message}");
        }

        var apiResp = JsonSerializer.Deserialize<ApiResponse<List<ServiceSerialize>>>(body);
        if (apiResp?.Data == null) return null;

        var dtos = apiResp.Data
            .Select(x => new ServiceDTO
                { ServiceId = x.ServiceId, ShortName = x.ShortName, ServiceTypeId = x.ServiceTypeId })
            .ToList();

        return dtos;
    }

    public async Task<CalculateShippingFeeResponse?> CalculateFeeAsync(CalculateShippingFeeRequest request)
    {
        _logger.Info("[GhnShippingService][CalculateFeeAsync] Calculating shipping fee.");
        var client = CreateClient();
        var payload = JsonSerializer.Serialize(request, _jsonOptions);
        var resp = await client.PostAsync(CALCULATE_FEE, new StringContent(payload, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error($"[GhnShippingService][CalculateFeeAsync] GHN error: {resp.StatusCode} - {body}");
            var errorCheck = JsonSerializer.Deserialize<ApiResponse<object>>(body, _jsonOptions);
            if (errorCheck == null)
            {
                _logger.Error("Error message: Invalid response from GHN.");
                return null;
            }

            _logger.Error($"Error message: {errorCheck?.CodeMessage}");
            throw ErrorHelper.BadRequest($"GHN error: {errorCheck?.Message}");
        }

        var apiResp = JsonSerializer.Deserialize<ApiResponse<CalculateShippingFeeResponse>>(body);
        return apiResp?.Data;
    }

    public GhnOrderRequest BuildGhnOrderRequest<T>(
    IEnumerable<T> items,
    Seller seller,
    Address toAddress,
    Func<T, Product> getProduct,
    Func<T, int> getQuantity)
    {
        var ghnOrderItems = items.Select(item =>
        {
            var product = getProduct(item);
            var category = product.Category;
            var length = Convert.ToInt32(product.Length ?? 10);
            var width = Convert.ToInt32(product.Width ?? 10);
            var height = Convert.ToInt32(product.Height ?? 10);
            var weight = Convert.ToInt32(product.Weight ?? 1000);

            return new GhnOrderItemDto
            {
                Name = product.Name,
                Code = product.Id.ToString(),
                Quantity = getQuantity(item),
                Price = Convert.ToInt32(product.Price),
                Length = length,
                Width = width,
                Height = height,
                Weight = weight,
                Category = new GhnItemCategory
                {
                    Level1 = category?.Name,
                    Level2 = category?.Parent?.Name
                }
            };
        }).ToList();

        _logger.Info($"Build GHN order request for seller {seller.Id} with {ghnOrderItems.Count} items.");

        return new GhnOrderRequest
        {
            PaymentTypeId = 2,
            Note = $"Giao hàng cho seller {seller.CompanyName}",
            RequiredNote = "CHOXEMHANGKHONGTHU",
            FromName = seller.CompanyName ?? "BlindTreasure Warehouse",
            FromPhone = "0925136907" ?? seller.CompanyPhone,
            FromAddress = seller.CompanyAddress ?? "72 Thành Thái, Phường 14, Quận 10, Hồ Chí Minh, TP.HCM",
            FromWardName = seller.CompanyWardName ?? "Phường 14",
            FromDistrictName = seller.CompanyDistrictName ?? "Quận 10",
            FromProvinceName = seller.CompanyProvinceName ?? "HCM",
            ToName = toAddress.FullName,
            ToPhone = toAddress.Phone,
            ToAddress = toAddress.AddressLine,
            ToWardName = toAddress.Ward ?? "",
            ToDistrictName = toAddress.District ?? "",
            ToProvinceName = toAddress.Province,
            CodAmount = 0,
            Content = $"Giao hàng cho {toAddress.FullName} từ seller {seller.CompanyName}",
            Length = ghnOrderItems.Max(i => i.Length),
            Width = ghnOrderItems.Max(i => i.Width),
            Height = ghnOrderItems.Max(i => i.Height),
            Weight = ghnOrderItems.Sum(i => i.Weight),
            InsuranceValue = ghnOrderItems.Sum(i => i.Price * i.Quantity) <= 5000000
                ? ghnOrderItems.Sum(i => i.Price * i.Quantity)
                : 5000000,
            ServiceTypeId = 2,
            Items = ghnOrderItems.ToArray()
        };
    }
}

public class CalculateShippingFeeRequest
{
    [JsonPropertyName("service_id")] public int? ServiceId { get; set; }

    [JsonPropertyName("service_type_id")] public int? ServiceTypeId { get; set; } = 2;

    [JsonPropertyName("from_district_id")] public int? FromDistrictId { get; set; }

    [JsonPropertyName("from_ward_code")] public string? FromWardCode { get; set; }

    [JsonPropertyName("to_district_id")] public int? ToDistrictId { get; set; }

    [JsonPropertyName("to_ward_code")] public string? ToWardCode { get; set; }

    [JsonPropertyName("weight")] public int? Weight { get; set; } = 1000;

    [JsonPropertyName("length")] public int? Length { get; set; } = 10;

    [JsonPropertyName("width")] public int? Width { get; set; } = 10;

    [JsonPropertyName("height")] public int? Height { get; set; } = 10;
}

public class CalculateShippingFeeResponse
{
    [JsonPropertyName("total")] public int Total { get; set; }

    [JsonPropertyName("service_fee")] public int ServiceFee { get; set; }

    [JsonPropertyName("insurance_fee")] public int InsuranceFee { get; set; }

    [JsonPropertyName("pick_station_fee")] public int PickStationFee { get; set; }

    [JsonPropertyName("coupon_value")] public int CouponValue { get; set; }

    [JsonPropertyName("r2s_fee")] public int R2sFee { get; set; }

    [JsonPropertyName("document_return")] public int DocumentReturn { get; set; }

    [JsonPropertyName("double_check")] public int DoubleCheck { get; set; }

    [JsonPropertyName("cod_fee")] public int CodFee { get; set; }

    [JsonPropertyName("pick_remote_areas_fee")]
    public int PickRemoteAreasFee { get; set; }

    [JsonPropertyName("deliver_remote_areas_fee")]
    public int DeliverRemoteAreasFee { get; set; }

    [JsonPropertyName("cod_failed_fee")] public int CodFailedFee { get; set; }
}

public class ApiResponse<T>
{
    [JsonPropertyName("code")] public int Code { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; }

    [JsonPropertyName("data")] public T Data { get; set; }

    [JsonPropertyName("code_message")] public string? CodeMessage { get; set; } = string.Empty;
    [JsonPropertyName("message_display")] public string? MessageDisplay { get; set; } = string.Empty;
}

public class ServiceSerialize
{
    [JsonPropertyName("service_id")] public int ServiceId { get; set; }
    [JsonPropertyName("short_name")] public string ShortName { get; set; }
    [JsonPropertyName("service_type_id")] public int ServiceTypeId { get; set; }
}

public class ServiceDTO
{
    public int ServiceId { get; set; }
    public string ShortName { get; set; }
    public int ServiceTypeId { get; set; }
}

public class ProvinceDto
{
    public int ProvinceID { get; set; }
    public string ProvinceName { get; set; }
    public string[]? NameExtension { get; set; } = Array.Empty<string>();
}

public class DistrictDto
{
    public int DistrictID { get; set; }
    public int ProvinceID { get; set; }
    public string DistrictName { get; set; }
    public string[]? NameExtension { get; set; } = Array.Empty<string>();
}

public class WardDto
{
    public string WardCode { get; set; }
    public int DistrictID { get; set; }
    public string WardName { get; set; }
    public string[]? NameExtension { get; set; } = Array.Empty<string>();
}