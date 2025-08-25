using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Entities;

public class Payout : BaseEntity
{
    // Thông tin cơ bản
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    // Kỳ thanh toán
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public PayoutPeriodType PeriodType { get; set; } = PayoutPeriodType.WEEKLY;

    // Tính toán tài chính
    public decimal GrossAmount { get; set; } // Tổng doanh thu (trước phí)
    public decimal PlatformFeeRate { get; set; } // Tỷ lệ phí nền tảng (VD: 5.0 = 5%)
    public decimal PlatformFeeAmount { get; set; } // Số tiền phí nền tảng
    public decimal NetAmount { get; set; } // Số tiền thực trả = Gross - Fee

    //Refund
    public decimal? TotalRefundAmount { get; set; } // Tổng refund trong period
    public decimal? AdjustedGrossAmount { get; set; } // Gross - Refunds

    // Stripe integration
    public string? StripeTransferId { get; set; }
    public string? StripeDestinationAccount { get; set; } // Seller's Stripe account

    // Trạng thái và thời gian
    public PayoutStatus Status { get; set; } = PayoutStatus.PROCESSING;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Metadata
    public string? Notes { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }
    //

    public List<string>? ProofImageUrls { get; set; } = new(); // new: khởi tạo mặc định tránh null

    // Navigation properties
    public ICollection<PayoutDetail> PayoutDetails { get; set; } = new List<PayoutDetail>();
    public ICollection<PayoutLog> PayoutLogs { get; set; } = new List<PayoutLog>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();

}