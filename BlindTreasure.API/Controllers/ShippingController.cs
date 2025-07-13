using BlindTreasure.Application.Utils;
using BlindTreasure.Domain;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlindTreasure.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShippingController : ControllerBase
    {
        private readonly BlindTreasureDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUnitOfWork _unitOfWork;
        private const string TOKEN = "28729d34-5751-11f0-9b81-222185cb68c8";
        private const string SHOP_ID = "197002";
        private const string BASE_URL = "https://dev-online-gateway.ghn.vn";
        private const string GET_PROVINCE = "/shiip/public-api/master-data/province";
        private const string GET_DISTRICT = "/shiip/public-api/master-data/district";
        private const string GET_WARD = "/shiip/public-api/master-data/ward";
        private const string GET_SERVICES = "/shiip/public-api/v2/shipping-order/available-services";
        private const string CALCULATE_FEE = "/shiip/public-api/v2/shipping-order/fee";
        private const string PREVIEW_ORDER = "/shiip/public-api/v2/shipping-order/preview";

        private readonly JsonSerializerOptions _jsonOptions;


        public ShippingController(IHttpClientFactory httpClientFactory, BlindTreasureDbContext context, IUnitOfWork unitOfWork  )
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _unitOfWork = unitOfWork;
            _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };

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

        [HttpGet("provinces")]
        public async Task<ActionResult<ApiResult<List<ProvinceDto>>>> GetProvinces()
        {
            var client = CreateClient();
            var resp = await client.GetAsync(GET_PROVINCE);
            if (!resp.IsSuccessStatusCode)
                return ApiResult<List<ProvinceDto>>.Failure("500", $"GHN error: {resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<List<ProvinceDto>>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ApiResult<List<ProvinceDto>>.Success(apiResp.Data);
        }

        [HttpGet("districts")]
        public async Task<ActionResult<ApiResult<List<DistrictDto>>>> GetDistricts([FromQuery] int provinceId)
        {
            var client = CreateClient();
            var resp = await client.GetAsync($"{GET_DISTRICT}?province_id={provinceId}");
            if (!resp.IsSuccessStatusCode)
                return ApiResult<List<DistrictDto>>.Failure("500", $"GHN error: {resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<List<DistrictDto>>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ApiResult<List<DistrictDto>>.Success(apiResp.Data);
        }

        [HttpGet("wards")]
        public async Task<ActionResult<ApiResult<List<WardDto>>>> GetWards([FromQuery] int districtId)
        {
            var client = CreateClient();
            var resp = await client.GetAsync($"{GET_WARD}?district_id={districtId}");
            if (!resp.IsSuccessStatusCode)
                return ApiResult<List<WardDto>>.Failure("500", $"GHN error: {resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<List<WardDto>>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ApiResult<List<WardDto>>.Success(apiResp.Data);
        }

        [HttpGet("available-services")]
        public async Task<ActionResult<ApiResult<List<ServiceDTO>>>> GetAvailableServices(
            [FromQuery] int fromDistrict, [FromQuery] int toDistrict)
        {
            var client = CreateClient();
            var resp = await client.GetAsync($"{GET_SERVICES}?shop_id={SHOP_ID}&from_district={fromDistrict}&to_district={toDistrict}");
            if (!resp.IsSuccessStatusCode)
                return ApiResult<List<ServiceDTO>>.Failure("500", $"GHN error: {resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<List<ServiceSerialize>>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var dtos = apiResp.Data
                .Select(x => new ServiceDTO { ServiceId = x.ServiceId, ShortName = x.ShortName, ServiceTypeId = x.ServiceTypeId })
                .ToList();

            return ApiResult<List<ServiceDTO>>.Success(dtos);
        }

        [HttpPost("calculate-fee")]
        public async Task<ActionResult<ApiResult<CalculateShippingFeeResponse>>> CalculateFee([FromBody] CalculateShippingFeeRequest request)
        {
            var client = CreateClient();
            var payload = JsonSerializer.Serialize(request);
            var resp = await client.PostAsync(CALCULATE_FEE, new StringContent(payload, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
                return ApiResult<CalculateShippingFeeResponse>.Failure("500", $"GHN error: {resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<CalculateShippingFeeResponse>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ApiResult<CalculateShippingFeeResponse>.Success(apiResp.Data);
        }

        [HttpPost("preview-order")]
        public async Task<ActionResult<ApiResult<GhnPreviewResponse>>> PreviewOrder([FromBody] GhnOrderRequest req)
        {
            var client = CreateClient();

            // Serialize request theo snake_case
            var payload = JsonSerializer.Serialize(req, _jsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Gọi GHN preview
            var resp = await client.PostAsync(PREVIEW_ORDER, content);
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine(body);    
            if (!resp.IsSuccessStatusCode)
                return ApiResult<GhnPreviewResponse>.Failure("500", $"GHN error: {resp.StatusCode}");

            // Đọc body và deserialize theo snake_case
            var apiResp = JsonSerializer.Deserialize<ApiResponse<GhnPreviewResponse>>(body, _jsonOptions);
            if (apiResp == null || apiResp.Data == null)
                return ApiResult<GhnPreviewResponse>.Failure("500", "Invalid response from GHN");

            return ApiResult<GhnPreviewResponse>.Success(apiResp.Data);
        }


        /// <summary>
        /// Tạo đơn hàng chính thức trên GHN.
        /// </summary>
        /// <param name="req">Thông tin đơn hàng (dùng chung với preview).</param>
        /// <returns>Kết quả tạo đơn hàng từ GHN.</returns>
        [HttpPost("create-order")]
        public async Task<ActionResult<ApiResult<GhnCreateResponse>>> CreateOrder([FromBody] GhnOrderRequest req)
        {
            var client = CreateClient();
            var payload = JsonSerializer.Serialize(req, _jsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/shiip/public-api/v2/shipping-order/create", content);
            if (!resp.IsSuccessStatusCode)
                return ApiResult<GhnCreateResponse>.Failure("500", $"GHN error: {resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<GhnCreateResponse>>(body, _jsonOptions);
            if (apiResp == null || apiResp.Data == null)
                return ApiResult<GhnCreateResponse>.Failure("500", "Invalid response from GHN");

            return ApiResult<GhnCreateResponse>.Success(apiResp.Data, "200", apiResp.Message);
        }
    }

    public class CalculateShippingFeeRequest
    {
        [JsonPropertyName("service_id")]
        public int? ServiceId { get; set; }

        [JsonPropertyName("service_type_id")]
        public int? ServiceTypeId { get; set; } = 2;

        [JsonPropertyName("from_district_id")]
        public int? FromDistrictId { get; set; }

        [JsonPropertyName("from_ward_code")]
        public string? FromWardCode { get; set; }

        [JsonPropertyName("to_district_id")]
        public int? ToDistrictId { get; set; }

        [JsonPropertyName("to_ward_code")]
        public string? ToWardCode { get; set; }

        [JsonPropertyName("weight")]
        public int? Weight { get; set; } = 1000;

        [JsonPropertyName("length")]
        public int? Length { get; set; } = 10;

        [JsonPropertyName("width")]
        public int? Width { get; set; } = 10;

        [JsonPropertyName("height")]
        public int? Height { get; set; } = 10;

    }

    public class CalculateShippingFeeResponse
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("service_fee")]
        public int ServiceFee { get; set; }

        [JsonPropertyName("insurance_fee")]
        public int InsuranceFee { get; set; }

        [JsonPropertyName("pick_station_fee")]
        public int PickStationFee { get; set; }

        [JsonPropertyName("coupon_value")]
        public int CouponValue { get; set; }

        [JsonPropertyName("r2s_fee")]
        public int R2sFee { get; set; }

        [JsonPropertyName("document_return")]
        public int DocumentReturn { get; set; }

        [JsonPropertyName("double_check")]
        public int DoubleCheck { get; set; }

        [JsonPropertyName("cod_fee")]
        public int CodFee { get; set; }

        [JsonPropertyName("pick_remote_areas_fee")]
        public int PickRemoteAreasFee { get; set; }

        [JsonPropertyName("deliver_remote_areas_fee")]
        public int DeliverRemoteAreasFee { get; set; }

        [JsonPropertyName("cod_failed_fee")]
        public int CodFailedFee { get; set; }
    }


    public class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public T Data { get; set; }
    }


    public class ServiceSerialize
    {
        [JsonPropertyName("service_id")]
        public int ServiceId { get; set; }
        [JsonPropertyName("short_name")]
        public string ShortName { get; set; }
        [JsonPropertyName("service_type_id")]
        public int ServiceTypeId { get; set; }
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
    }

    public class DistrictDto
    {
        public int DistrictID { get; set; }
        public int ProvinceID { get; set; }
        public string DistrictName { get; set; }
    }

    public class WardDto
    {
        public string WardCode { get; set; }
        public int DistrictID { get; set; }
        public string WardName { get; set; }
    }
}
