using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class GhtkFeeResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("fee")]
        public GhtkFeeInfo? Fee { get; set; } = null!;
        public string? StatusCode { get; set; } // For error cases (422, etc.)
        public string? LogId { get; set; }      // For error cases
    }

    public class GhtkFeeInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("fee")]
        public int ShippingFee { get; set; }

        [JsonPropertyName("insurance_fee")]
        public int InsuranceFee { get; set; }

        [JsonPropertyName("delivery_type")]
        public string DeliveryType { get; set; } = default!;

        [JsonPropertyName("a")]
        public int A { get; set; }

        [JsonPropertyName("dt")]
        public string Dt { get; set; } = default!;

        [JsonPropertyName("extFees")]
        public List<GhtkExtraFee> ExtFees { get; set; } = new();

        [JsonPropertyName("delivery")]
        public bool Delivery { get; set; }
    }

    public class GhtkExtraFee
    {
        [JsonPropertyName("display")]
        public string Display { get; set; } = default!;

        [JsonPropertyName("title")]
        public string Title { get; set; } = default!;

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = default!;
    }
}