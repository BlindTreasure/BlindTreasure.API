using BlindTreasure.Domain.DTOs.TransactionDTOs;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.PaymentDTOs;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal NetAmount { get; set; }
    public string Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? PaymentIntentId { get; set; }
    public DateTime PaidAt { get; set; }
    public decimal RefundedAmount { get; set; } = 0;
    public List<TransactionDto> Transactions { get; set; } = new(); // Thêm dòng này
}