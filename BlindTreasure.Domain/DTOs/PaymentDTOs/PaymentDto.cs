using BlindTreasure.Domain.DTOs.TransactionDTOs;

namespace BlindTreasure.Domain.DTOs.PaymentDTOs;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal NetAmount { get; set; }
    public string Method { get; set; }
    public string Status { get; set; }
    public string TransactionId { get; set; }
    public DateTime PaidAt { get; set; }
    public decimal RefundedAmount { get; set; } = 0;
    public List<TransactionDto> Transactions { get; set; } = new(); // Thêm dòng này
}