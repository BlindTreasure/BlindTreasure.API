using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.PayoutDTOs;

public class PayoutCalculationRequestDto
{
    [Required] public DateTime PeriodStart { get; set; }

    [Required] public DateTime PeriodEnd { get; set; }

    public PayoutPeriodType PeriodType { get; set; } = PayoutPeriodType.WEEKLY;

    public decimal? CustomPlatformFeeRate { get; set; } // Override default fee rate

    //public List<Guid>? SpecificSellerIds { get; set; } // Nếu chỉ muốn tính cho một số seller cụ thể
}

// Response DTO cho kết quả tính payout
public class PayoutCalculationResultDto
{
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;
    public string? StripeAccountId { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal PlatformFeeRate { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal NetAmount { get; set; }

    public int TotalOrderDetails { get; set; }
    public int TotalOrders { get; set; }

    public bool CanPayout { get; set; } // Có thể payout không (có Stripe account, amount > min threshold)
    public string? PayoutBlockReason { get; set; }

    public List<PayoutDetailSummaryDto> OrderDetailSummaries { get; set; } = new();
}

// Chi tiết một OrderDetail trong payout
public class PayoutDetailSummaryDto
{
    public Guid OrderDetailId { get; set; }
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal ContributedAmount { get; set; }
    public DateTime OrderCompletedAt { get; set; }
}

// Response DTO cho danh sách payouts
public class PayoutListResponseDto
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PeriodType { get; set; } = string.Empty;

    public decimal GrossAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PlatformFeeAmount { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? StripeTransferId { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
}

// Chi tiết một payout
public class PayoutDetailResponseDto
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PeriodType { get; set; } = string.Empty;

    public decimal GrossAmount { get; set; }
    public decimal PlatformFeeRate { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal NetAmount { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? StripeTransferId { get; set; }
    public string? StripeDestinationAccount { get; set; }
    public string? Notes { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }

    public List<PayoutDetailSummaryDto> PayoutDetails { get; set; } = new();
    public List<PayoutLogDto> PayoutLogs { get; set; } = new();
}

// Payout log DTO
public class PayoutLogDto
{
    public Guid Id { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredByUserName { get; set; }
    public DateTime LoggedAt { get; set; }
}

// Request DTO để thực hiện payout
public class ProcessPayoutRequestDto
{
    [Required] public Guid PayoutId { get; set; }

    public string? Notes { get; set; }
    public bool ForceProcess { get; set; } = false; // Bỏ qua validation nếu cần
}

// Bulk process payout request
public class BulkProcessPayoutRequestDto
{
    [Required] [MinLength(1)] public List<Guid> PayoutIds { get; set; } = new();

    public string? Notes { get; set; }
    public bool ForceProcess { get; set; } = false;
}

// Payout statistics DTO
public class PayoutStatisticsDto
{
    public int TotalPayouts { get; set; }
    public int PendingPayouts { get; set; }
    public int ProcessingPayouts { get; set; }
    public int CompletedPayouts { get; set; }
    public int FailedPayouts { get; set; }

    public decimal TotalGrossAmount { get; set; }
    public decimal TotalNetAmount { get; set; }
    public decimal TotalPlatformFees { get; set; }

    public decimal AveragePayoutAmount { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }

    public List<PayoutPeriodSummaryDto> PeriodSummaries { get; set; } = new();
}

public class PayoutPeriodSummaryDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int PayoutCount { get; set; }
    public decimal TotalAmount { get; set; }
}