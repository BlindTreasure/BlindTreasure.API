using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class GhtkSubmitOrderRequest
    {
        [JsonPropertyName("products")]
        public List<GhtkProduct> Products { get; set; }

        [JsonPropertyName("order")]
        public GhtkOrderInfo Order { get; set; }
    }

    public class GhtkProduct
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("weight")] public double Weight { get; set; }
        [JsonPropertyName("quantity")] public int Quantity { get; set; }
        [JsonPropertyName("product_code")] public string ProductCode { get; set; }
    }

    public class GhtkOrderInfo //dựa trên docs api đăng đơn hàng của GHTK
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("pick_name")]
        public string PickName { get; set; } = default!;

        [JsonPropertyName("pick_address")]
        public string PickAddress { get; set; } = default!;

        [JsonPropertyName("pick_province")]
        public string PickProvince { get; set; } = default!;

        [JsonPropertyName("pick_district")]
        public string PickDistrict { get; set; } = default!;

        [JsonPropertyName("pick_ward")]
        public string PickWard { get; set; } = default!;

        [JsonPropertyName("pick_tel")]
        public string PickTel { get; set; } = default!;

        [JsonPropertyName("tel")]
        public string Tel { get; set; } = default!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("address")]
        public string Address { get; set; } = default!;

        [JsonPropertyName("province")]
        public string Province { get; set; } = default!;

        [JsonPropertyName("district")]
        public string District { get; set; } = default!;

        [JsonPropertyName("ward")]
        public string Ward { get; set; } = default!;

        [JsonPropertyName("hamlet")]
        public string Hamlet { get; set; } = default!;

        [JsonPropertyName("is_freeship")]
        public int IsFreeship { get; set; }

        [JsonPropertyName("pick_date")]
        public DateTimeOffset PickDate { get; set; }

        [JsonPropertyName("pick_money")]
        public long PickMoney { get; set; }

        [JsonPropertyName("note")]
        public string Note { get; set; } = default!;

        [JsonPropertyName("value")]
        public long Value { get; set; }

        [JsonPropertyName("transport")]
        public string Transport { get; set; } = default!;

        [JsonPropertyName("pick_option")]
        public string PickOption { get; set; } = default!;

        [JsonPropertyName("deliver_option")]
        public string DeliverOption { get; set; } = default!;
    }

 
}
