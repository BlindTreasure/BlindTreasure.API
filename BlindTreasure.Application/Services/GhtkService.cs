using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

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

        public GhtkService(HttpClient client, ILoggerService logger, IMapperService mapper, IOrderService orderService, IUnitOfWork unitOfWork, IOptions<GhtkSettings> opt)
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

            _logger.Info($"GHTK Settings: BaseUrl={_ghtkSettings.BaseUrl}, ApiToken={_ghtkSettings.ApiToken}, PartnerCode={_ghtkSettings.PartnerCode}");

        }


        public async Task<GhtkAuthResponse> AuthenticateAsync()
        {
            var response = await _client.PostAsync("/services/authenticated", null);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadFromJsonAsync<GhtkAuthResponse>();
                errorContent.StatusCode = response.StatusCode.ToString();
                _logger.Error($"Authentication failed: {System.Text.Json.JsonSerializer.Serialize(errorContent)}");
                return errorContent;
            }
            var result = await response.Content.ReadFromJsonAsync<GhtkAuthResponse>();
            result.StatusCode = response.StatusCode.ToString();
            if (result == null)
            {
                _logger.Error("Empty response from GHTK authentication API.");
                return new GhtkAuthResponse { Success = false, Message = "Empty response" };
            }
            _logger.Success("Authentication successfully.");
            return result;


            // chưa dùng tới code ở dưới 
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.Error("Authentication failed: Invalid token or credentials.");
                return new GhtkAuthResponse { Success = false, Message = "Invalid token or credentials" };

            }
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.Error("Authentication failed: Unauthorized access.");
                return new GhtkAuthResponse { Success = false, Message = "Unauthorized access" };
            }
            response.EnsureSuccessStatusCode();
            _logger.Success("Authentication successfully.");

           

        }

        public async Task<GhtkSubmitOrderResponse> SubmitOrderAsync(
    GhtkSubmitOrderRequest request)
        {
            var response = await _client.PostAsJsonAsync(
                "/services/shipment/order", request);

            var result = await response.Content
                .ReadFromJsonAsync<GhtkSubmitOrderResponse>();

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"GHTK submit-order failed: {result?.Message}");
            }

            return result;
        }

        public async Task<GhtkTrackResponse> TrackOrderAsync(string trackingOrder)
        {
            var response = await _client.GetAsync($"/services/shipment/v2/{Uri.EscapeDataString(trackingOrder)}");

            var result = await response.Content.ReadFromJsonAsync<GhtkTrackResponse>()
                       ?? new GhtkTrackResponse { Success = false, Message = "Empty response" };

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"GHTK track-order failed: {result.Message}");
            }

            return result;
        }
    }
}
