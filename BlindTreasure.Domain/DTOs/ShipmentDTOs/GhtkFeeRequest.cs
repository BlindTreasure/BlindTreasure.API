using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class GhtkFeeRequest
    {
        // optional
        public string? PickAddressId { get; set; }
        public string? PickAddress { get; set; }

        // required
        public string PickProvince { get; set; } = default!;
        public string PickDistrict { get; set; } = default!;

        // optional
        public string? PickWard { get; set; }
        public string? PickStreet { get; set; }
        public string? Address { get; set; }

        // required
        public string Province { get; set; } = default!;
        public string District { get; set; } = default!;

        // optional
        public string? Ward { get; set; }
        public string? Street { get; set; }

        // required
        public int Weight { get; set; }

        // optional
        public int? Value { get; set; }
        public string? Transport { get; set; }
        public string DeliverOption { get; set; } = default!;
        public string[]? Tags { get; set; }
    }
}
