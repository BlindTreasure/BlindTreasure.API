using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.StripeDTOs
{
    public class PayoutRequestDto
    {
        public string SellerStripeAccountId { get; set; }
        public decimal Amount { get; set; }
        [DefaultValue("usd")]
        public string Currency { get; set; } = "usd";
        [DefaultValue("Payout to seller")]

        public string Description { get; set; } = "Payout to seller";
    }
}
