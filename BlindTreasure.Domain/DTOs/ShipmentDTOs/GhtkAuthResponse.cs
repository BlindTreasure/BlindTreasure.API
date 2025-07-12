using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class GhtkAuthResponse
    {
        public string StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        [JsonPropertyName("log_id")]
        public string LogId { get; set; }
        public object Data { get; set; }
    }
}
