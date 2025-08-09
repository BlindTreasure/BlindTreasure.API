using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Payment : BaseEntity
{
    // FK → Order
    public Guid OrderId { get; set; }
    public Order Order { get; set; }

    public decimal Amount { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal NetAmount { get; set; }
    public string Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? PaymentIntentId { get; set; }
    public DateTime PaidAt { get; set; }
    public decimal RefundedAmount { get; set; } = 0;
    //Suggested
    //// Quan hệ 1-n với Order (cho thanh toán nhóm)
    //public ICollection<Order> GroupOrders { get; set; } = new List<Order>();

    //// Nhóm các order liên quan (dùng cho thanh toán chung)
    //public Guid? CheckoutGroupId { get; set; }


    // 1-n → Transactions
    public ICollection<Transaction> Transactions { get; set; }
}