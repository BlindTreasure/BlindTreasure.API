using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs;

public class GhtkSubmitOrderResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("order")] public required SubmitOrderResponseOrder Order { get; set; }
}

public class SubmitOrderResponseOrder
{
    [JsonPropertyName("partner_id")] public required string PartnerId { get; set; }

    [JsonPropertyName("label")] public string Label { get; set; } = default!;

    [JsonPropertyName("area")] public string Area { get; set; }

    [JsonPropertyName("fee")] public string Fee { get; set; }

    [JsonPropertyName("insurance_fee")] public string InsuranceFee { get; set; }

    [JsonPropertyName("tracking_id")] public string TrackingId { get; set; } = default!;

    [JsonPropertyName("estimated_pick_time")]
    public string EstimatedPickTime { get; set; } = default!;

    [JsonPropertyName("estimated_deliver_time")]
    public string EstimatedDeliverTime { get; set; } = default!;

    [JsonPropertyName("products")] public GhtkProduct[] Products { get; set; } = Array.Empty<GhtkProduct>();

    [JsonPropertyName("status_id")] public int StatusId { get; set; }
}