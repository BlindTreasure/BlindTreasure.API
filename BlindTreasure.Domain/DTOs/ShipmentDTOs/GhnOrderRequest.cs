using System;
using System.ComponentModel;
using System.Collections.Generic;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class GhnOrderRequest
    {
        [DefaultValue(2)]
        public int PaymentTypeId { get; set; } = 2;

        [DefaultValue("cai nay de test ne")]
        public string? Note { get; set; }

        [DefaultValue("CHOXEMHANGKHONGTHU")]
        public string RequiredNote { get; set; } = "CHOXEMHANGKHONGTHU";

        public string? ReturnPhone { get; set; }
        public string? ReturnAddress { get; set; }
        public int? ReturnDistrictId { get; set; }
        public string? ReturnWardCode { get; set; }
        public string? ClientOrderCode { get; set; }

        public string FromName { get; set; }
        public string FromPhone { get; set; }
        public string FromAddress { get; set; }
        public string FromWardName { get; set; }
        public string FromDistrictName { get; set; }
        public string FromProvinceName { get; set; }

        public string ToName { get; set; }
        public string ToPhone { get; set; }
        public string ToAddress { get; set; }
        public string ToWardName { get; set; }
        public string ToDistrictName { get; set; }
        public string ToProvinceName { get; set; }

        [DefaultValue(0)]
        public int CodAmount { get; set; } = 0;

        public string? Content { get; set; }

        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Weight { get; set; }

        public int? CodFailedAmount { get; set; }
        public int? PickStationId { get; set; }
        public int? DeliverStationId { get; set; }

        [DefaultValue(0)]
        public int InsuranceValue { get; set; } = 0;

        [DefaultValue(2)]
        public int ServiceTypeId { get; set; } = 2;

        public string? Coupon { get; set; }
        public long? PickupTime { get; set; }
        public int[]? PickShift { get; set; }
        public GhnOrderItemDto[]? Items { get; set; }
    }

    public class GhnOrderItemDto
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Weight { get; set; }
        public GhnItemCategory Category { get; set; }
    }

    public class GhnItemCategory
    {
        public string Level1 { get; set; }
        public string? Level2 { get; set; }
        public string? Level3 { get; set; }
    }
}
