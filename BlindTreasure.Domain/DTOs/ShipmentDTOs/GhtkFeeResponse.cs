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
        public string Message { get; set; }

        [JsonPropertyName("fee")]
        public GhtkFeeData Fee { get; set; }
    }

    public class GhtkFeeData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("fee")]
        public int ShippingFee { get; set; }

        [JsonPropertyName("insurance_fee")]
        public int InsuranceFee { get; set; }

        [JsonPropertyName("delivery")]
        public bool Delivery { get; set; }
    }
}
