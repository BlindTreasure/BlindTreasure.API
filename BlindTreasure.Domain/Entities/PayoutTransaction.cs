using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Entities;

public class PayoutTransaction : BaseEntity
{
    // FK → Payout
    public Guid PayoutId { get; set; }
    public Payout Payout { get; set; }

    // Seller info (denormalized for quick access)
    public Guid SellerId { get; set; }
    public string SellerName { get; set; }
   // public Seller? Seller { get; set; }

    // Stripe info
    public string StripeTransferId { get; set; }
    public string StripeDestinationAccount { get; set; }
    public string? StripeBalanceTransactionId { get; set; }

    // Transaction details
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string Status { get; set; } // "pending", "succeeded", "failed"
    public DateTime? TransferredAt { get; set; }

    // Context & tracking
    public string? Description { get; set; }
    public string? FailureReason { get; set; }
    public Guid InitiatedBy { get; set; } = Guid.Empty;
    public string InitiatedByName { get; set; } = "System";

    // Optional
    public string? ExternalRef { get; set; }
    public string? BatchId { get; set; }

    //platform
    public decimal? PlatformRevenueOfPayoutAmount { get; set; } // Số tiền doanh thu từ payout nền tảng
}