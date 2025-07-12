using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{

    public class GhtkTrackResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("order")]
        public GhtkTrackOrder Order { get; set; } = default!;
    }

    public class GhtkTrackOrder
    {
        [JsonPropertyName("label_id")]
        public string LabelId { get; set; } = default!;

        [JsonPropertyName("partner_id")]
        public string PartnerId { get; set; } = default!;

        [JsonPropertyName("status")]
        public int? Status { get; set; } = default!;

        [JsonPropertyName("status_text")]
        public string StatusText { get; set; } = default!;

        [JsonPropertyName("created")]
        public string Created { get; set; } = default!;

        [JsonPropertyName("modified")]
        public string Modified { get; set; } = default!;

        [JsonPropertyName("message")]
        public string Note { get; set; } = default!;

        [JsonPropertyName("pick_date")]
        public string PickDate { get; set; } = default!;

        [JsonPropertyName("deliver_date")]
        public string DeliverDate { get; set; } = default!;

        [JsonPropertyName("customer_fullname")]
        public string CustomerFullName { get; set; } = default!;

        [JsonPropertyName("customer_tel")]
        public string CustomerTel { get; set; } = default!;

        [JsonPropertyName("address")]
        public string Address { get; set; } = default!;

        [JsonPropertyName("storage_day")]
        public int? StorageDay { get; set; } = default!;

        [JsonPropertyName("ship_money")]
        public int? ShipMoney { get; set; } = default!;

        [JsonPropertyName("insurance")]
        public int? Insurance { get; set; } = default!;

        [JsonPropertyName("value")]
        public int? Value { get; set; } = default!;

        [JsonPropertyName("weight")]
        public int? Weight { get; set; } = default!;

        [JsonPropertyName("pick_money")]
        public int? PickMoney { get; set; }

        [JsonPropertyName("is_freeship")]
        public int? IsFreeShip { get; set; } = default!;
    }
}