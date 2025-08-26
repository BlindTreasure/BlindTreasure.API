using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.DTOs.PayoutDTOs;

// Base DTO for payout info
public abstract class PayoutBaseDto
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
    public List<string>? ProofImageUrls { get; set; } = new();
}

// List DTO
public class PayoutListResponseDto : PayoutBaseDto
{
    // Add more list-specific fields if needed
}

// Detail DTO
public class PayoutDetailResponseDto : PayoutBaseDto
{
    public string SellerEmail { get; set; } = string.Empty;
    public decimal PlatformFeeRate { get; set; }
    public string? StripeDestinationAccount { get; set; }
    public string? Notes { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public List<PayoutDetailSummaryDto> PayoutDetails { get; set; } = new();
    public List<PayoutLogDto> PayoutLogs { get; set; } = new();
}

// Calculation DTO
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
    public bool CanPayout { get; set; }
    public string? PayoutBlockReason { get; set; }
    public List<string>? ProofImageUrls { get; set; } = new();
    public List<PayoutDetailSummaryDto> OrderDetailSummaries { get; set; } = new();
}

// Detail summary DTO
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

// Log DTO
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

// Request DTOs remain unchanged
public class PayoutCalculationRequestDto : PayoutPeriodTimeRequestDto
{
    public PayoutPeriodType PeriodType { get; set; } = PayoutPeriodType.WEEKLY;
    public decimal? CustomPlatformFeeRate { get; set; }
}

public class PayoutPeriodTimeRequestDto
{
    [Required] public DateTime PeriodStart { get; set; }
    [Required] public DateTime PeriodEnd { get; set; }
}

public class ProcessPayoutRequestDto
{
    [Required] public Guid PayoutId { get; set; }
    public string? Notes { get; set; }
    public bool ForceProcess { get; set; } = false;
}

public class BulkProcessPayoutRequestDto
{
    [Required] [MinLength(1)] public List<Guid> PayoutIds { get; set; } = new();
    public string? Notes { get; set; }
    public bool ForceProcess { get; set; } = false;
}

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