using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs;

public class GhnPreviewResponse
{
    [JsonPropertyName("order_code")] public string OrderCode { get; set; }
    [JsonPropertyName("sort_code")] public string SortCode { get; set; }
    [JsonPropertyName("trans_type")] public string TransType { get; set; }
    [JsonPropertyName("fee")] public GhnFee? Fee { get; set; }
    [JsonPropertyName("total_fee")] public decimal? TotalFee { get; set; }

    [JsonPropertyName("expected_delivery_time")]
    public DateTime ExpectedDeliveryTime { get; set; }
}

public class GhnFee
{
    [JsonPropertyName("main_service")] public int MainService { get; set; }
    [JsonPropertyName("insurance")] public int Insurance { get; set; }
    [JsonPropertyName("station_do")] public int StationDo { get; set; }
    [JsonPropertyName("station_pu")] public int StationPu { get; set; }
    [JsonPropertyName("return")] public int Return { get; set; }
    [JsonPropertyName("r2s")] public int R2s { get; set; }
    [JsonPropertyName("coupon")] public int Coupon { get; set; }
    [JsonPropertyName("cod_failed_fee")] public int CodFailedFee { get; set; }
}

public class GhnCreateResponse : GhnPreviewResponse
{
    [JsonPropertyName("message_display")] public string MessageDisplay { get; set; }
}