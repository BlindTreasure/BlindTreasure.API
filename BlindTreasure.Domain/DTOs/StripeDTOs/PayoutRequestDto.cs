using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.StripeDTOs;

public class PayoutRequestDto
{
    public string SellerStripeAccountId { get; set; }
    public decimal Amount { get; set; }

    [DefaultValue("usd")] public string Currency { get; set; } = "usd";

    [DefaultValue("Payout to seller")] public string Description { get; set; } = "Payout to seller";
}