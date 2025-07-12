using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Stripe.Forwarding;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace BlindTreasure.Application.Services
{
    public class GhtkService : IGhtkService
    {
        private readonly HttpClient _client;
        private readonly ILoggerService _logger;
        private readonly IMapperService _mapper;
        private readonly IOrderService _orderService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GhtkSettings _ghtkSettings;
        private readonly JsonSerializerOptions _jsonOptions;

        public GhtkService(
            HttpClient client,
            ILoggerService logger,
            IMapperService mapper,
            IOrderService orderService,
            IUnitOfWork unitOfWork,
            IOptions<GhtkSettings> opt)
        {
            _ghtkSettings = opt.Value;
            _client = client;
            _client.BaseAddress = new Uri(_ghtkSettings.BaseUrl);
            _client.DefaultRequestHeaders.Add("Token", _ghtkSettings.ApiToken);
            _client.DefaultRequestHeaders.Add("X-Client-Source", _ghtkSettings.PartnerCode);
            _logger = logger;
            _mapper = mapper;
            _orderService = orderService;
            _unitOfWork = unitOfWork;
            _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

            _logger.Info($"GHTK Settings: BaseUrl={_ghtkSettings.BaseUrl}, ApiToken={_ghtkSettings.ApiToken}, PartnerCode={_ghtkSettings.PartnerCode}");
        }

        public async Task<GhtkAuthResponse> AuthenticateAsync()
        {
            try
            {
                var response = await _client.PostAsync("/services/authenticated", null);
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GhtkAuthResponse>(responseContent, _jsonOptions)
                             ?? new GhtkAuthResponse { Success = false, Message = "Empty response" };
                result.StatusCode = response.StatusCode.ToString();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Error($"Authentication failed: {responseContent}");
                }
                else
                {
                    _logger.Success("Authentication successfully.");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return new GhtkAuthResponse { Success = false, Message = ex.Message, StatusCode = "500" };
            }
        }

        public async Task<GhtkSubmitOrderResponse> SubmitOrderAsync(GhtkSubmitOrderRequest request)
        {
            try
            {
                var content = JsonSerializer.Serialize(request, _jsonOptions);
                var response = await _client.PostAsync("/services/shipment/order", new StringContent(content, Encoding.UTF8, "application/json"));
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.Info(responseContent);

                // Try to parse as GhtkSubmitOrderResponse first
                var result = JsonSerializer.Deserialize<GhtkSubmitOrderResponse>(responseContent, _jsonOptions);

                // If failed or error, try to parse as GhtkAuthResponse for error details
                if (result == null || result.Success == false)
                {
                    var errorResult = JsonSerializer.Deserialize<GhtkAuthResponse>(responseContent, _jsonOptions);
                    return new GhtkSubmitOrderResponse
                    {
                        Success = errorResult?.Success ?? false,
                        Message = errorResult?.Message ?? "Unknown error",
                        StatusCode = errorResult?.StatusCode ?? response.StatusCode.ToString()
                    };
                }

                result.StatusCode = response.StatusCode.ToString();
                if (!response.IsSuccessStatusCode || !result.Success)
                {
                    _logger.Error($"GHTK submit-order failed: {result.Message}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return new GhtkSubmitOrderResponse { Success = false, Message = ex.Message, StatusCode = "500" };
            }
        }

        public async Task<GhtkTrackResponse> TrackOrderAsync(string trackingOrder)
        {
            try
            {
                var response = await _client.GetAsync($"/services/shipment/v2/{Uri.EscapeDataString(trackingOrder)}");
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.Info(responseContent);
                var result = JsonSerializer.Deserialize<GhtkTrackResponse>(responseContent, _jsonOptions)
                             ?? new GhtkTrackResponse { Success = false, Message = "Empty response" };

                if (!response.IsSuccessStatusCode || !result.Success)
                {
                    _logger.Error($"GHTK track-order failed: {result.Message}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return new GhtkTrackResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<GhtkFeeResponse> CalculateFeeAsync(GhtkFeeRequest request)
        {
            try
            {
                var query = BuildFeeQueryString(request);
                _logger.Info($"url is : {query}");
                var response = await _client.GetAsync($"/services/shipment/fee{query}");
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.Info(responseContent);

                var result = JsonSerializer.Deserialize<GhtkFeeResponse>(responseContent, _jsonOptions)
                             ?? new GhtkFeeResponse { Success = false, Message = "Empty response" };

                if (!response.IsSuccessStatusCode || !result.Success)
                {
                    _logger.Error($"GHTK fee calculation failed: {result.Message}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return new GhtkFeeResponse { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// Build query string từ GhtkFeeRequest.
        /// </summary>
        private static string BuildFeeQueryString(GhtkFeeRequest req)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);

            if (!string.IsNullOrWhiteSpace(req.PickAddressId)) query["pick_address_id"] = req.PickAddressId;
            if (!string.IsNullOrWhiteSpace(req.PickAddress)) query["pick_address"] = req.PickAddress;
            query["pick_province"] = req.PickProvince;
            query["pick_district"] = req.PickDistrict;
            if (!string.IsNullOrWhiteSpace(req.PickWard)) query["pick_ward"] = req.PickWard;
            if (!string.IsNullOrWhiteSpace(req.PickStreet)) query["pick_street"] = req.PickStreet;
            if (!string.IsNullOrWhiteSpace(req.Address)) query["address"] = req.Address;
            query["province"] = req.Province;
            query["district"] = req.District;
            if (!string.IsNullOrWhiteSpace(req.Ward)) query["ward"] = req.Ward;
            if (!string.IsNullOrWhiteSpace(req.Street)) query["street"] = req.Street;
            query["weight"] = req.Weight.ToString();
            if (req.Value.HasValue) query["value"] = req.Value.Value.ToString();
            if (!string.IsNullOrWhiteSpace(req.Transport)) query["transport"] = req.Transport;
            query["deliver_option"] = req.DeliverOption;
            if (req.Tags != null && req.Tags.Length > 0)
            {
                foreach (var tag in req.Tags)
                    query.Add("tags", tag);
            }

            return "?" + query.ToString();
        }
    }
}