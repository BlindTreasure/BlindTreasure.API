using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.DTOs.PayoutDTOs;

namespace BlindTreasure.Application.Mappers;

public static class PayoutDtoMapper
{
    public static PayoutTransactionDto ToPayoutTransactionDto(PayoutTransaction entity)
    {
        return new PayoutTransactionDto
        {
            PayoutId = entity.PayoutId,
            Payout = entity.Payout != null ? ToPayoutBaseDto(entity.Payout) : null,
            SellerId = entity.SellerId,
            SellerName = entity.SellerName,
            StripeTransferId = entity.StripeTransferId,
            StripeDestinationAccount = entity.StripeDestinationAccount,
            StripeBalanceTransactionId = entity.StripeBalanceTransactionId,
            Amount = entity.Amount,
            Currency = entity.Currency,
            Status = entity.Status,
            TransferredAt = entity.TransferredAt,
            Description = entity.Description,
            FailureReason = entity.FailureReason,
            InitiatedBy = entity.InitiatedBy == Guid.Empty ? null : entity.InitiatedBy,
            InitiatedByName = entity.InitiatedByName,
            ExternalRef = entity.ExternalRef,
            BatchId = entity.BatchId,
            PlatformRevenueOfPayoutAmount = entity.PlatformRevenueOfPayoutAmount
        };
    }

    public static PayoutListResponseDto ToPayoutBaseDto(Payout entity)
    {
        return new PayoutListResponseDto
        {
            Id = entity.Id,
            SellerId = entity.SellerId,
            SellerName = entity.Seller?.CompanyName ?? string.Empty,
            PeriodStart = entity.PeriodStart,
            PeriodEnd = entity.PeriodEnd,
            PeriodType = entity.PeriodType.ToString(),
            GrossAmount = entity.GrossAmount,
            NetAmount = entity.NetAmount,
            PlatformFeeAmount = entity.PlatformFeeAmount,
            Status = entity.Status.ToString(),
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt,
            CompletedAt = entity.CompletedAt,
            StripeTransferId = entity.StripeTransferId,
            FailureReason = entity.FailureReason,
            RetryCount = entity.RetryCount,
            ProofImageUrls = entity.ProofImageUrls ?? new List<string>()
        };
    }
}